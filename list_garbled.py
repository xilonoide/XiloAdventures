# -*- coding: utf-8 -*-
from pathlib import Path
text = Path('XiloAdventures.Wpf/Windows/WorldEditorWindow.xaml.cs').read_text(encoding='utf-8')
for idx,line in enumerate(text.splitlines(),1):
    if '\u00c3' in line or '\u00c2' in line or '¿' in line or 'Ã' in line:
        print(idx, repr(line))
