module github.com/ekucher/bravo-bis-configurator

go 1.26.5

require golang.org/x/text v0.41.0

require (
	github.com/lxn/walk v0.0.0-20210112085537-c389da54e794
	golang.org/x/sys v0.47.0
	gopkg.in/yaml.v3 v3.0.1
)

require (
	github.com/lxn/win v0.0.0-20210218163916-a377121e959e // indirect
	gopkg.in/Knetic/govaluate.v3 v3.0.0 // indirect
)

replace github.com/lxn/walk => ./third_party/walk
