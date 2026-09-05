#!/usr/bin/env bash
# Renders the BlastScale music from MIDI with a General MIDI SoundFont and writes the OGG files the
# Unity client loads from Assets/Resources/Audio.
#
#   ./tools/music/render.sh path/to/GeneralUser-GS.sf2 [output-dir]
#
# Requirements: python3, fluidsynth, ffmpeg, oggenc (brew install fluid-synth ffmpeg vorbis-tools).
# SoundFont: GeneralUser GS by S. Christian Collins (https://github.com/mrbumpy409/GeneralUser-GS),
# free to use in any project; not committed to the repository because of its size.
set -euo pipefail

SF2="${1:?path to a .sf2 SoundFont}"
OUT="${2:-unity-client/Assets/Resources/Audio}"
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
RATE=44100
BPM=120   # must match compose.py; 16 bars = 32 s = 1 411 200 samples = 22 050 blocks of 64
BARS=16

python3 "$HERE/compose.py" "$WORK"

# Chorus stays off: its LFO phase drifts between passes and would break the seamless cut below.
render() { # midi -> wav with a light hall, gain 0.6 leaves headroom for the peak normalisation
  fluidsynth -ni -q -g 0.6 -r "$RATE" \
    -o synth.reverb.active=1 -o synth.reverb.room-size=0.55 -o synth.reverb.width=0.8 -o synth.reverb.level=0.45 \
    -o synth.chorus.active=0 -o synth.polyphony=256 \
    -F "$2" "$SF2" "$1"
}

render "$WORK/music_main.mid" "$WORK/main_raw.wav"
render "$WORK/jingle_win.mid"  "$WORK/win_raw.wav"
render "$WORK/jingle_lose.mid" "$WORK/lose_raw.wav"

# The loop is pass two of three identical passes: bars 16-32. By then reverb and note tails are in
# steady state, so the last sample of the cut flows into its first sample without a seam.
START=$(python3 -c "print(int(round($BARS*4*60/$BPM*$RATE)))")
END=$((START * 2))
python3 - "$WORK/main_raw.wav" "$START" "$END" <<'PY'
import sys, wave, array
path, start, end = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
with wave.open(path) as w:
    ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
    data = array.array('h', w.readframes(n))
def frame(i): return data[i*ch:(i+1)*ch]
window = rate  # compare one second after the cut start with one second after the cut end
diff = max(abs(a-b) for i in range(window) for a, b in zip(frame(start+i), frame(end+i)))
peak = max(abs(v) for v in data[start*ch:end*ch])
print(f"loop check: frames={n} cut={start}..{end} ({(end-start)/rate:.3f}s) max sample difference across the seam={diff} peak={peak}")
assert diff <= 160, "the two passes differ: the loop would click"
PY

normalise() { # peak-normalise to -1 dBFS (ffmpeg) and encode Vorbis with the reference encoder (oggenc)
  local gain
  gain=$(ffmpeg -hide_banner -i "$1" -af "$2,volumedetect" -f null - 2>&1 | sed -n 's/.*max_volume: \([-0-9.]*\) dB.*/\1/p')
  local db
  db=$(python3 -c "print(round(-1.0 - ($gain), 2))")
  ffmpeg -hide_banner -loglevel error -y -i "$1" -af "$2,volume=${db}dB" -c:a pcm_s16le "$WORK/normalised.wav"
  oggenc -Q -q 5 -o "$3" "$WORK/normalised.wav"
  echo "$(basename "$3"): gain ${db} dB, $(stat -f%z "$3") bytes"
}

mkdir -p "$OUT"
normalise "$WORK/main_raw.wav" "atrim=start_sample=$START:end_sample=$END,asetpts=PTS-STARTPTS" "$OUT/music_main.ogg"
normalise "$WORK/win_raw.wav"  "silenceremove=stop_periods=1:stop_threshold=-60dB:stop_silence=0.4,anull" "$OUT/jingle_win.ogg"
normalise "$WORK/lose_raw.wav" "silenceremove=stop_periods=1:stop_threshold=-60dB:stop_silence=0.4,anull" "$OUT/jingle_lose.ogg"
rm -rf "$WORK"
