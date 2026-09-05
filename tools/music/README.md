# Music pipeline

The in-game music and the win/lose jingles are real instrument recordings, produced from MIDI with
a General MIDI SoundFont rather than shipped as opaque audio files. Everything needed to regenerate
them is here:

| File | Role |
|------|------|
| `compose.py` | writes `music_main.mid`, `jingle_win.mid`, `jingle_lose.mid` with a dependency-free MIDI writer; the arrangement (chord cycle, marimba melody, electric piano comping, bass, glockenspiel, pad, drums) lives in this file |
| `render.sh` | renders the MIDI with FluidSynth, cuts the seamless loop, peak-normalises and encodes Vorbis into `unity-client/Assets/Resources/Audio/` |
| `GeneralUser-GS-LICENSE.txt` | licence of the SoundFont used for the shipped files |

```bash
brew install fluid-synth ffmpeg vorbis-tools
curl -L -o /tmp/GeneralUser-GS.sf2 https://github.com/mrbumpy409/GeneralUser-GS/raw/main/GeneralUser-GS.sf2
./tools/music/render.sh /tmp/GeneralUser-GS.sf2
```

## Instruments

[GeneralUser GS](https://github.com/mrbumpy409/GeneralUser-GS) by S. Christian Collins — a free
General MIDI SoundFont that may be used without restriction, commercially or not (see the licence
file). It is downloaded at render time and not committed (32 MB).

## How the loop stays seamless

`compose.py` writes the 16-bar song three times back to back with identical notes and velocities
(the humanisation RNG is re-seeded per pass). `render.sh` cuts exactly the middle pass — bars 16 to
32, sample-accurate — so the reverb tails at the start of the cut are the tails of the previous
pass and the end of the cut flows straight into its own beginning. The script compares one second
of audio after the cut start with one second after the cut end and refuses to continue if they
differ. Two details make that hold: chorus (an LFO whose phase drifts between passes) is switched
off, and the tempo is 120 BPM so that a pass is exactly 32.000 s — FluidSynth's player schedules
events on a millisecond / 64-sample grid, and at 112 BPM the passes landed on different grid
offsets and came out shifted by a block.

## Arrangement notes

120 BPM, C major, `| C | Am | F | G | C | Am | Dm | G |` played twice: section A states the marimba
hook, section B answers an octave up over shaker sixteenths and lands on the tonic so the loop
closes. The electric piano comps broken chords on the off-beats, the bass walks root – octave –
fifth – root, the glockenspiel doubles the last note of every second bar, and a warm pad glues the
chords underneath. The win jingle is a rising glockenspiel run into a C major fanfare with strings;
the lose jingle is a four-note marimba descent into a soft A minor pad.
