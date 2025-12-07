# -*- coding: utf-8 -*-
from pathlib import Path
text = Path('XiloAdventures.Wpf/Windows/WorldEditorWindow.xaml.cs').read_text(encoding='utf-8')
for i,line in enumerate(text.splitlines(), 1):
    if 'Ã' in line or 'Â' in line:
        print(i, line)
