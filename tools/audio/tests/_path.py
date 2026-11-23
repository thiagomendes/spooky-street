"""Puts tools/audio on sys.path so tests import `spookyaudio` from any working directory."""
import sys
from pathlib import Path

AUDIO = Path(__file__).resolve().parent.parent
REPO = AUDIO.parent.parent
if str(AUDIO) not in sys.path:
    sys.path.insert(0, str(AUDIO))
