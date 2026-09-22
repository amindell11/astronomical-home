"""Stand-in for the imageio_ffmpeg wheel so assemble.py tests never need a real ffmpeg.

Put this directory first on PYTHONPATH. get_ffmpeg_exe() returns a sentinel, and
subprocess.run is wrapped to emulate ffmpeg for commands that start with it:

- encode (-f concat): the concat list must exist; a fake clip is written holding
  the list's entry count, so read-back can echo the truth.
- read-back (-f null): prints "frame=N" the way ffmpeg's summary line does, where
  N is the count stored in the clip unless FAKE_FFMPEG_READBACK overrides it:
  an integer reports that count, "error" makes the decode exit nonzero.

FAKE_FFMPEG_ENCODE=error makes the encode itself exit nonzero without a clip.
"""
import os
import subprocess

FFMPEG = "<fake-ffmpeg>"
_real_run = subprocess.run


def get_ffmpeg_exe():
    return FFMPEG


def _fake_ffmpeg(cmd):
    if "concat" in cmd:
        if os.environ.get("FAKE_FFMPEG_ENCODE") == "error":
            return 1, b"fake ffmpeg: encode refused\n"
        list_path = cmd[cmd.index("-i") + 1]
        with open(list_path, encoding="utf-8") as f:
            entries = sum(1 for line in f if line.startswith("file "))
        with open(cmd[-1], "w", encoding="utf-8") as f:
            f.write(str(entries))
        return 0, b""
    if "null" in cmd:
        override = os.environ.get("FAKE_FFMPEG_READBACK", "")
        if override == "error":
            return 1, b"fake ffmpeg: moov atom not found\n"
        with open(cmd[cmd.index("-i") + 1], encoding="utf-8") as f:
            stored = f.read()
        count = override or stored
        return 0, ("frame=%5s fps=0.0 q=-0.0 Lsize=N/A\n" % count).encode()
    raise AssertionError("fake ffmpeg got an unexpected command: %r" % (cmd,))


def _run(cmd, *args, **kwargs):
    if not (isinstance(cmd, (list, tuple)) and cmd and cmd[0] == FFMPEG):
        return _real_run(cmd, *args, **kwargs)
    returncode, stderr = _fake_ffmpeg(list(cmd))
    if returncode != 0 and kwargs.get("check"):
        raise subprocess.CalledProcessError(returncode, cmd, output=b"", stderr=stderr)
    return subprocess.CompletedProcess(cmd, returncode, stdout=b"", stderr=stderr)


subprocess.run = _run
