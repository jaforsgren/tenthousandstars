GODOT   := /Applications/Godot_mono.app/Contents/MacOS/Godot
CLI     := dotnet run --project tools/cli/Tts.Cli.csproj --

.PHONY: build build-godot test cli narrative combat clean

build:
	dotnet build Tts.csproj

build-godot:
	$(GODOT) --headless --build-solutions

test:
	dotnet test tests/Tts.Tests.csproj

# Simulate campaign progression. Optional WIN_PATTERN e.g. make narrative WIN_PATTERN=W,L,W
narrative:
	$(CLI) $(WIN_PATTERN)

# Simulate a combat roll. Usage: make combat ARGS="10 8 1.2"
combat:
	$(CLI) combat $(ARGS)

clean:
	dotnet clean Tts.csproj
	dotnet clean tests/Tts.Tests.csproj
	dotnet clean tools/cli/Tts.Cli.csproj
