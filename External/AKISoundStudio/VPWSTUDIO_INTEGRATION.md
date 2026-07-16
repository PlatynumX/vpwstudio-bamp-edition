# VPWStudio BAMP sound-editor integration

The source in this directory is built as the bundled sound editor used by
VPWStudio BAMP's Project > Sounds command.

Upstream:
- PlatynumX/AKISoundStudio

Runtime package layout:

    VPWStudio.exe
    SoundEditor/
      AKISoundStudio.exe
      data/

VPWStudio launches AKISoundStudio.exe and passes the current project's input
ROM path as the first command-line argument.
