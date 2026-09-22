import re, io

for v, card in (('V1', 'e19cf55667234a2cb1fc1189bace0011'),
                ('V2', '7a592719359185a4a93c1b0e060257ad')):
    p = 'D:/Codex project/Dragon/Assets/DragonBound/UI/Variants/%s/Scenes/Greybox_Main.unity' % v
    txt = io.open(p, encoding='utf-8', errors='replace').read()
    blocks = re.split(r'\n--- ', txt)
    go = {}
    for b in blocks:
        m = re.match(r'!u!(\d+) &(\d+)', b)
        if m and m.group(1) == '1':
            nm = re.search(r'\n  m_Name: (.*)', b)
            go[m.group(2)] = nm.group(1).strip() if nm else '?'
    for b in blocks:
        m = re.match(r'!u!(\d+) &(\d+)', b)
        if not m or m.group(1) != '114':
            continue
        if 'enemyViewPrefab' not in b and 'enemyViewTemplate' not in b:
            continue
        g = re.search(r'\n  m_GameObject: \{fileID: (\d+)\}', b)
        name = go.get(g.group(1), '?') if g else '?'
        script = re.search(r'\n  m_Script: \{fileID: \d+, guid: (\w+)', b)

        def val(k):
            mm = re.search(r'\n  %s: \{fileID: \d+, guid: (\w+)' % k, b)
            return mm.group(1) if mm else None

        print(v, '|', name, '| script=', script.group(1) if script else '?',
              '| template=', val('enemyViewTemplate'),
              '| prefab=', val('enemyViewPrefab'),
              '| expect EnemyCard=', card)
