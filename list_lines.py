# -*- coding: utf-8 -*-
from pathlib import Path
text = Path('XiloAdventures.Wpf/Windows/WorldEditorWindow.xaml.cs').read_text(encoding='utf-8')
for idx, line in enumerate(text.splitlines(), 1):
    if idx in {63,378,430,537,681,761,781,920,934,955,964,987,1153,1176,1288,1289,1370,1424,1447,1470,1494,1522,1642,1687,1716,2084}:
        print(idx, line.encode('unicode_escape'))
