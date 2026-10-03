from pathlib import Path
p=Path('scratch/capture/pr-body.md')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('The ticket remains open for the human art-direction decision.', 'The user selected B and requested thicker contours. B now defaults to 3.2 pixels (previously 1.6); the local review page retains both widths for comparison. The ticket remains open for the remaining production implications.')
s=s.replace('No art direction is selected and #685 is not closed by this PR.', 'B is the selected direction; production rollout is not included and #685 is not closed by this PR.')
p.write_text(s,encoding='utf-8')
