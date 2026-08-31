package validate

import (
	"fmt"
	"os"
	"regexp"
	"strconv"
	"strings"

	"github.com/ekucher/bravo-bis-configurator/internal/schema"
)

// typeCoercionError checks that value can be interpreted as t, returning
// (message, true) if not. TypeString/TypePath/TypeEnum accept any string —
// enum membership is enforced separately by a RuleEnum, not by the type
// itself, since not every enum-typed field necessarily has a rule
// attached in every schema.
func typeCoercionError(t schema.FieldType, value string) (string, bool) {
	switch t {
	case schema.TypeInt:
		if _, err := strconv.ParseInt(strings.TrimSpace(value), 10, 64); err != nil {
			return fmt.Sprintf("value %q is not a valid integer", value), true
		}
	case schema.TypeFloat:
		if _, err := strconv.ParseFloat(strings.TrimSpace(value), 64); err != nil {
			return fmt.Sprintf("value %q is not a valid number", value), true
		}
	case schema.TypeBool:
		if _, err := parseBool(value); err != nil {
			return fmt.Sprintf("value %q is not a valid boolean (expected 0/1/true/false)", value), true
		}
	case schema.TypeString, schema.TypePath, schema.TypeEnum:
		// Any string is structurally valid; semantic checks are rule-driven.
	}
	return "", false
}

// parseBool accepts the value conventions actually observed in the real
// bravo.ini/bis.ini samples (0/1) plus the common true/false spelling, all
// case-insensitively.
func parseBool(value string) (bool, error) {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "0", "false":
		return false, nil
	case "1", "true":
		return true, nil
	default:
		return false, fmt.Errorf("not a boolean: %q", value)
	}
}

// checkRule runs r against value, returning (message, true) if it fails.
func checkRule(r *schema.ValidationRule, value string) (string, bool) {
	switch r.Kind {
	case schema.RulePathExists:
		return checkPathExists(r, value)
	case schema.RuleRegex:
		return checkRegex(r, value)
	case schema.RuleEnum:
		return checkEnum(r, value)
	case schema.RuleRange:
		return checkRange(r, value)
	default:
		return fmt.Sprintf("internal error: unknown validation rule kind %q", r.Kind), true
	}
}

func checkPathExists(r *schema.ValidationRule, value string) (string, bool) {
	if strings.TrimSpace(value) == "" {
		return "path is empty", true
	}
	info, err := os.Stat(value)
	if err != nil {
		return fmt.Sprintf("path does not exist: %s", value), true
	}
	switch r.PathMode {
	case schema.PathModeFile:
		if info.IsDir() {
			return fmt.Sprintf("expected a file but found a directory: %s", value), true
		}
	case schema.PathModeDir:
		if !info.IsDir() {
			return fmt.Sprintf("expected a directory but found a file: %s", value), true
		}
	case schema.PathModeEither, "":
		// no further check
	}
	return "", false
}

func checkRegex(r *schema.ValidationRule, value string) (string, bool) {
	re, err := regexp.Compile(r.Pattern)
	if err != nil {
		return fmt.Sprintf("internal error: invalid regex pattern %q: %v", r.Pattern, err), true
	}
	if !re.MatchString(value) {
		return fmt.Sprintf("value %q does not match required pattern %q", value, r.Pattern), true
	}
	return "", false
}

func checkEnum(r *schema.ValidationRule, value string) (string, bool) {
	for _, v := range r.Values {
		if v == value {
			return "", false
		}
	}
	return fmt.Sprintf("value %q is not one of the allowed values %v", value, r.Values), true
}

func checkRange(r *schema.ValidationRule, value string) (string, bool) {
	n, err := strconv.ParseFloat(strings.TrimSpace(value), 64)
	if err != nil {
		return fmt.Sprintf("value %q is not numeric, cannot check range", value), true
	}
	if r.Min != nil && n < *r.Min {
		return fmt.Sprintf("value %v is below the minimum %v", n, *r.Min), true
	}
	if r.Max != nil && n > *r.Max {
		return fmt.Sprintf("value %v is above the maximum %v", n, *r.Max), true
	}
	return "", false
}
