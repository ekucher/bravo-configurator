// Command configurator is the GUI configurator-validator for bravo.ini
// (server) and bis.ini (client). It also offers a --validate CLI mode
// (see runValidate) that reuses the same internal/ini + internal/schema +
// internal/validate packages the GUI does, with no GUI dependency, for
// scripted/CI use.
package main

import (
	"flag"
	"fmt"
	"io"
	"os"

	"github.com/ekucher/bravo-bis-configurator/internal/app"
	"github.com/ekucher/bravo-bis-configurator/internal/ini"
	"github.com/ekucher/bravo-bis-configurator/internal/profile"
	"github.com/ekucher/bravo-bis-configurator/internal/schema"
	"github.com/ekucher/bravo-bis-configurator/internal/validate"
)

func main() {
	os.Exit(run(os.Args[1:], os.Stdout, os.Stderr))
}

// run is the whole CLI/dispatch surface, factored out from main so it can
// be exercised by tests without touching real os.Stdout/os.Exit.
func run(args []string, stdout, stderr io.Writer) int {
	fs := flag.NewFlagSet("configurator", flag.ContinueOnError)
	fs.SetOutput(stderr)

	validateMode := fs.Bool("validate", false, "validate a file and print findings, without opening the GUI")
	profileName := fs.String("profile", "", `profile to use: "bravo" (server) or "bis" (client)`)
	filePath := fs.String("file", "", "path to the ini file to validate")
	schemaPath := fs.String("schema", "", "path to a custom schema YAML file (overrides the bundled default for this run)")
	encodingFlag := fs.String("encoding", "", `force a text encoding instead of auto-detecting: "utf-8", "utf-8-bom", "windows-1251", or "windows-1252"`)

	if err := fs.Parse(args); err != nil {
		return 2
	}

	if !*validateMode {
		if err := app.RunGUI(); err != nil {
			fmt.Fprintf(stderr, "gui error: %v\n", err)
			return 1
		}
		return 0
	}

	return runValidate(validateArgs{
		profileName:   *profileName,
		filePath:      *filePath,
		schemaPath:    *schemaPath,
		forceEncoding: ini.Encoding(*encodingFlag),
	}, stdout, stderr)
}

type validateArgs struct {
	profileName   string
	filePath      string
	schemaPath    string
	forceEncoding ini.Encoding
}

// runValidate loads the requested (or custom) schema, parses filePath, and
// prints every finding. It exits non-zero only when at least one
// SeverityError finding is present, so DRAFT-schema warnings never fail a
// scripted check on their own.
func runValidate(a validateArgs, stdout, stderr io.Writer) int {
	if a.profileName == "" || a.filePath == "" {
		fmt.Fprintln(stderr, "--validate requires --profile and --file")
		return 2
	}
	prof, ok := profile.Find(a.profileName)
	if !ok {
		fmt.Fprintf(stderr, "unknown profile %q (expected \"bravo\" or \"bis\")\n", a.profileName)
		return 2
	}

	var s *schema.Schema
	var err error
	if a.schemaPath != "" {
		s, err = schema.Load(a.schemaPath)
	} else {
		s, err = prof.LoadSchema()
	}
	if err != nil {
		fmt.Fprintf(stderr, "schema error: %v\n", err)
		return 2
	}

	doc, _, err := ini.ReadFile(a.filePath, ini.DefaultParseOptions(), a.forceEncoding)
	if err != nil {
		fmt.Fprintf(stderr, "read error: %v\n", err)
		return 2
	}

	if s.Status == schema.StatusDraft {
		fmt.Fprintf(stdout, "NOTE: the %q schema is DRAFT/unverified; results below may be incomplete. See docs/SCHEMA_STATUS.md.\n", prof.Name)
	}

	results := validate.Validate(doc, s)
	if len(results) == 0 {
		fmt.Fprintln(stdout, "OK: no findings.")
		return 0
	}
	for _, r := range results {
		fmt.Fprintf(stdout, "[%s] %s.%s: %s\n", r.Severity, r.Section, r.Key, r.Message)
	}
	if validate.HasErrors(results) {
		return 1
	}
	return 0
}
