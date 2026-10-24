# Setup EXE

`ElecKit.iss` is the Inno Setup source for the selectable-version Windows installer. GitHub Actions compiles the setup executable to `EXE/dist/ElecKit-Setup.exe` after all version builds and tests pass. The generated EXE is published as the `ElecKit-Setup-EXE` workflow artifact; generated binaries are not committed.