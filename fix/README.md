# Hangar fix acceptance

Implementation commit: 21ed6c2e.

The old tree failed both assertions in red-summary.json. The fixed tree passed 28 routed hangar tests and 20 fresh-process graphics tests. All ten live pointer clicks changed their intended option. At 2560x1408, the preview target is 1601 square with 4x MSAA and uses about 127.11 MiB rather than 506.25 MiB. The code-quality review was clean, and the ReSharper ratchet passed with zero blocking findings.

Raw probe reports, test summaries, cold-run NUnit XML, and analyzer summary are in this folder. The composite UI capture is taken through Unity ScreenCapture, which includes the overlay canvas. Timing data from the original diagnosis is not a precise GPU-only benchmark.
