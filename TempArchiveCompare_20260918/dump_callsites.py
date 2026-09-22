import re, io

p = 'D:/Codex project/Dragon/Assets/DragonBound/Runtime/Presentation/CombatFxView.cs'
lines = io.open(p, encoding='utf-8', errors='replace').read().split('\n')

hits = [609, 917, 1125, 1386, 1567, 2422]
for h in hits:
    # walk upwards to nearest method signature
    for i in range(h - 1, max(0, h - 120), -1):
        s = lines[i]
        if re.match(r'\s*(private|public|protected|internal)\s+[\w<>\[\],\s\.]+\s+\w+\s*\(', s) and not s.strip().startswith('//'):
            print('line %-5d -> %-6d %s' % (h, i + 1, s.strip()))
            break
