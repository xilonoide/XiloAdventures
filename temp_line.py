# -*- coding: utf-8 -*-
from pathlib import Path
text = Path('XiloAdventures.Wpf/Windows/WorldEditorWindow.xaml.cs').read_text(encoding='latin1')
print(repr(text.splitlines()[377]))
