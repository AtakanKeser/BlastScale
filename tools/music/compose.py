#!/usr/bin/env python3
"""Composes the BlastScale music as Standard MIDI files, with no third-party dependencies.

The tracks are rendered with FluidSynth and a General MIDI SoundFont by render.sh; this file only
produces the notes. Everything is deterministic (seeded humanisation), so re-running it yields the
same bytes.

Outputs (in the directory given as the first argument):
  music_main.mid   the in-game loop: a bright, laid-back casual-puzzle groove in C major, 120 BPM,
                   16 bars (8-bar chord cycle played twice with a varied melody). It is written
                   three times back to back so the renderer can cut the middle pass as a seamless
                   loop (reverb and note tails are already in steady state there).
  jingle_win.mid   a short fanfare for a cleared level
  jingle_lose.mid  a soft descending sting for a lost level
"""
import random
import struct
import sys
from pathlib import Path

TICKS_PER_BEAT = 480
BPM = 120  # 64 beats = exactly 32.000 s: keeps every pass on the same millisecond/64-sample grid


# ------------------------------------------------------------------------- tiny MIDI writer
class Track:
    """Collects absolute-time MIDI events and serialises them as one MIDI track chunk."""

    def __init__(self, channel, program=None, volume=100, pan=64, reverb=40, chorus=0, name=""):
        self.channel = channel
        self.events = []  # (tick, order, bytes)
        if name:
            self.meta(0, 0x03, name.encode("ascii"))
        if program is not None:
            self.add(0, bytes([0xC0 | channel, program]))
        self.control(0, 7, volume)
        self.control(0, 10, pan)
        self.control(0, 91, reverb)
        self.control(0, 93, chorus)

    def add(self, tick, data, order=1):
        self.events.append((int(tick), order, data))

    def meta(self, tick, kind, payload):
        self.add(tick, bytes([0xFF, kind]) + varlen(len(payload)) + payload, order=0)

    def control(self, tick, controller, value):
        self.add(tick, bytes([0xB0 | self.channel, controller, max(0, min(127, value))]), order=0)

    def note(self, tick, pitch, length, velocity):
        """A note; note-offs are ordered before note-ons at the same tick so repeated notes retrigger."""
        velocity = max(1, min(127, int(velocity)))
        self.add(tick, bytes([0x90 | self.channel, pitch, velocity]), order=2)
        self.add(tick + max(1, int(length)), bytes([0x80 | self.channel, pitch, 64]), order=1)

    def chunk(self):
        self.events.sort(key=lambda e: (e[0], e[1]))
        out = bytearray()
        last = 0
        for tick, _, data in self.events:
            out += varlen(tick - last) + data
            last = tick
        out += varlen(0) + bytes([0xFF, 0x2F, 0x00])  # end of track
        return b"MTrk" + struct.pack(">I", len(out)) + bytes(out)


def varlen(value):
    """MIDI variable-length quantity."""
    buf = [value & 0x7F]
    value >>= 7
    while value:
        buf.append((value & 0x7F) | 0x80)
        value >>= 7
    return bytes(reversed(buf))


def write_midi(path, tracks, bpm=BPM):
    tempo = Track(0, name="tempo")
    tempo.events = []
    tempo.meta(0, 0x51, struct.pack(">I", int(60_000_000 / bpm))[1:])
    tempo.meta(0, 0x58, bytes([4, 2, 24, 8]))  # 4/4
    chunks = [tempo.chunk()] + [t.chunk() for t in tracks]
    header = b"MThd" + struct.pack(">IHHH", 6, 1, len(chunks), TICKS_PER_BEAT)
    Path(path).write_bytes(header + b"".join(chunks))


# ------------------------------------------------------------------------- musical helpers
B = TICKS_PER_BEAT           # a quarter note
E = B // 2                   # an eighth
S = B // 4                   # a sixteenth
BAR = 4 * B

NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def p(name):
    """'C4' -> MIDI pitch."""
    letter, octave = name[0], int(name[-1])
    accidental = 1 if "#" in name else (-1 if "b" in name[1:] else 0)
    return 12 * (octave + 1) + NOTE[letter] + accidental


rng = random.Random(2026)


def human(velocity, spread=6):
    """Small deterministic velocity variation so nothing sounds machine-stamped."""
    return velocity + rng.randint(-spread, spread)


# 8-bar chord cycle, one entry per bar: (root name, chord tones for the comping, bass root)
CYCLE = [
    ("C", ["C4", "E4", "G4"], "C2"),
    ("Am", ["A3", "C4", "E4"], "A1"),
    ("F", ["F3", "A3", "C4"], "F2"),
    ("G", ["G3", "B3", "D4"], "G2"),
    ("C", ["C4", "E4", "G4"], "C2"),
    ("Am", ["A3", "C4", "E4"], "A1"),
    ("Dm", ["D4", "F4", "A4"], "D2"),
    ("G", ["G3", "B3", "D4"], "G2"),
]

# Marimba melody, per bar of the 16-bar form: list of (offset in eighths, pitch, length in eighths).
# Section A (bars 1-8) states the hook; section B (bars 9-16) answers it higher and busier, then
# lands back on the tonic so the loop closes.
MELODY = [
    # --- A ---
    [(0, "E5", 1), (1, "G5", 1), (2, "A5", 1), (3, "G5", 2), (5, "E5", 1), (6, "D5", 2)],
    [(0, "C5", 2), (2, "E5", 1), (3, "A5", 1), (4, "G5", 2), (6, "E5", 1), (7, "D5", 1)],
    [(0, "C5", 1), (1, "A4", 1), (2, "C5", 2), (4, "F5", 1), (5, "E5", 1), (6, "D5", 2)],
    [(0, "B4", 2), (2, "D5", 1), (3, "G5", 1), (4, "B5", 2), (6, "A5", 1), (7, "G5", 1)],
    [(0, "E5", 1), (1, "G5", 1), (2, "A5", 1), (3, "C6", 2), (5, "A5", 1), (6, "G5", 2)],
    [(0, "E5", 2), (2, "C5", 1), (3, "E5", 1), (4, "A5", 3), (7, "G5", 1)],
    [(0, "F5", 1), (1, "A5", 1), (2, "D6", 2), (4, "C6", 1), (5, "A5", 1), (6, "F5", 2)],
    [(0, "G5", 1), (1, "B5", 1), (2, "D6", 1), (3, "B5", 1), (4, "G5", 2), (6, "D5", 1), (7, "E5", 1)],
    # --- B ---
    [(0, "G5", 1), (1, "E5", 1), (2, "C6", 1), (3, "E6", 2), (5, "D6", 1), (6, "C6", 2)],
    [(0, "A5", 1), (1, "C6", 1), (2, "E6", 2), (4, "C6", 1), (5, "A5", 1), (6, "G5", 2)],
    [(0, "A5", 2), (2, "F5", 1), (3, "A5", 1), (4, "C6", 2), (6, "A5", 1), (7, "F5", 1)],
    [(0, "G5", 1), (1, "D6", 1), (2, "B5", 1), (3, "G5", 1), (4, "D5", 2), (6, "G5", 1), (7, "A5", 1)],
    [(0, "C6", 2), (2, "G5", 1), (3, "E5", 1), (4, "G5", 2), (6, "A5", 1), (7, "C6", 1)],
    [(0, "E6", 1), (1, "C6", 1), (2, "A5", 2), (4, "E5", 1), (5, "G5", 1), (6, "A5", 2)],
    [(0, "F5", 1), (1, "A5", 1), (2, "D6", 1), (3, "F6", 1), (4, "D6", 2), (6, "A5", 1), (7, "C6", 1)],
    [(0, "B5", 1), (1, "D6", 1), (2, "G5", 2), (4, "E5", 1), (5, "D5", 1), (6, "C5", 2)],
]


def add_song_pass(tracks, start_bar):
    """Writes one 16-bar pass of the song starting at bar `start_bar`.

    The humanisation RNG is re-seeded per pass so every pass is note-for-note identical; that is what
    lets render.sh cut the middle pass as a seamless loop (a different velocity anywhere would make
    the seam audible).
    """
    rng.seed(2026)
    marimba, epiano, bass, glock, pad, drums = tracks
    for bar_index in range(16):
        bar_start = (start_bar + bar_index) * BAR
        root, chord, bass_root = CYCLE[bar_index % 8]
        chord_p = [p(n) for n in chord]
        second_half = bar_index >= 8

        # marimba: the melody, slightly detached (85 % of the written length)
        for offset, name, length in MELODY[bar_index]:
            vel = human(96 if offset in (0, 4) else 82)
            marimba.note(bar_start + offset * E, p(name), int(length * E * 0.85), vel)

        # electric piano: broken chord on the off-beats (the "and" of 1 and 3) plus a soft downbeat
        epiano.note(bar_start, chord_p[0], E, human(58))
        for beat in (0, 2):
            for i, pitch in enumerate(chord_p):
                epiano.note(bar_start + beat * B + E + i * S // 2, pitch, B, human(64 if beat == 0 else 58))
        # a top voice an octave up on beat 4 in the B section for lift
        if second_half:
            epiano.note(bar_start + 3 * B, chord_p[2] + 12, E, human(52))

        # bass: root on 1, octave on the "and" of 2, fifth on 3, root on the "and" of 4
        r = p(bass_root)
        bass.note(bar_start, r, B - S, human(96))
        bass.note(bar_start + B + E, r + 12, E, human(72))
        bass.note(bar_start + 2 * B, r + 7, B - S, human(88))
        bass.note(bar_start + 3 * B + E, r, E, human(76))

        # glockenspiel: sparkle on the last beat of every second bar, mirroring the melody's last note
        if bar_index % 2 == 1:
            last = MELODY[bar_index][-1]
            glock.note(bar_start + 3 * B + E, p(last[1]) + 12, E, human(58, 4))
        if bar_index == 15:
            glock.note(bar_start + 3 * B, p("E6"), E, 70)
            glock.note(bar_start + 3 * B + E, p("G6"), E, 74)

        # pad: whole-bar chord, very soft, an octave down for warmth
        for pitch in chord_p:
            pad.note(bar_start, pitch - 12, BAR - S, human(38, 3))

        # drums (channel 10): hi-hat eighths with accents, kick on 1 and the "and" of 2, side stick on 2/4,
        # shaker sixteenths in the B section, a tambourine hit on beat 4 of every fourth bar
        for eighth in range(8):
            drums.note(bar_start + eighth * E, 42, S, human(74 if eighth % 2 == 0 else 52, 4))
        drums.note(bar_start, 36, E, human(100, 3))
        drums.note(bar_start + B + E, 36, E, human(84, 3))
        drums.note(bar_start + 2 * B, 36, E, human(96, 3))
        drums.note(bar_start + B, 37, E, human(70, 4))
        drums.note(bar_start + 3 * B, 37, E, human(66, 4))
        if second_half:
            for sixteenth in range(16):
                if sixteenth % 2 == 1:
                    drums.note(bar_start + sixteenth * S, 82, S // 2, human(46, 5))
        if bar_index % 4 == 3:
            drums.note(bar_start + 3 * B, 54, E, human(72, 4))
        if bar_index == 15:  # a tiny fill into the loop point
            drums.note(bar_start + 3 * B + E, 38, S, 78)
            drums.note(bar_start + 3 * B + E + S, 38, S, 88)


def compose_main(out_dir):
    marimba = Track(0, program=12, volume=104, pan=62, reverb=48, name="marimba")
    epiano = Track(1, program=4, volume=78, pan=76, reverb=52, chorus=24, name="electric piano")
    bass = Track(2, program=33, volume=100, pan=64, reverb=18, name="fingered bass")
    glock = Track(3, program=9, volume=70, pan=50, reverb=64, name="glockenspiel")
    pad = Track(4, program=89, volume=48, pan=64, reverb=70, chorus=30, name="warm pad")
    drums = Track(9, program=None, volume=92, pan=64, reverb=30, name="drums")
    tracks = [marimba, epiano, bass, glock, pad, drums]
    # Three passes: render.sh cuts pass two (bars 16-32) as the seamless loop.
    for pass_index in range(3):
        add_song_pass(tracks, pass_index * 16)
    write_midi(out_dir / "music_main.mid", tracks)


def tail_track(seconds, bpm):
    """A muted channel holding one inaudible note, so the renderer keeps going for the reverb tail."""
    tail = Track(15, program=0, volume=0, pan=64, reverb=0, name="tail")
    ticks = int(seconds * bpm / 60 * TICKS_PER_BEAT)
    tail.note(ticks - 10, 24, 10, 1)
    return tail


def compose_win(out_dir):
    """Two-bar fanfare: a rising arpeggio into a bright major chord with a glockenspiel sparkle."""
    glock = Track(0, program=9, volume=100, pan=60, reverb=70, name="glockenspiel")
    strings = Track(1, program=48, volume=80, pan=64, reverb=72, name="strings")
    epiano = Track(2, program=4, volume=84, pan=70, reverb=56, name="electric piano")
    drums = Track(9, program=None, volume=90, pan=64, reverb=40, name="drums")
    tracks = [glock, strings, epiano, drums]
    run = ["C5", "E5", "G5", "C6", "E6", "G6"]
    for i, name in enumerate(run):
        glock.note(i * S, p(name), E, 90 + i * 4)
    for i, name in enumerate(["C6", "G6", "E6", "C7"]):
        glock.note(6 * S + i * E, p(name), B, 108)
    for pitch in ("C4", "E4", "G4", "C5"):
        strings.note(6 * S, p(pitch), 2 * B, 84)
        strings.note(6 * S + 2 * B, p(pitch), 2 * B, 72)
    for pitch in ("C4", "E4", "G4"):
        epiano.note(0, p(pitch), 6 * S, 70)
    for pitch in ("F4", "A4", "C5"):
        epiano.note(6 * S, p(pitch), B, 88)
    for pitch in ("C4", "E4", "G4", "C5"):
        epiano.note(6 * S + B, p(pitch), 2 * B, 92)
    drums.note(6 * S, 49, B, 100)   # crash
    drums.note(6 * S, 36, E, 100)
    drums.note(6 * S + B, 54, E, 70)
    tracks.append(tail_track(4.0, 120))
    write_midi(out_dir / "jingle_win.mid", tracks, bpm=120)


def compose_lose(out_dir):
    """A soft, sympathetic descent: marimba steps down into a minor chord held by the pad."""
    marimba = Track(0, program=12, volume=96, pan=62, reverb=60, name="marimba")
    pad = Track(1, program=89, volume=70, pan=64, reverb=76, chorus=20, name="warm pad")
    tracks = [marimba, pad]
    for i, name in enumerate(["E5", "D5", "C5", "B4"]):
        marimba.note(i * E, p(name), E, 84 - i * 6)
    marimba.note(4 * E, p("A4"), B, 72)
    marimba.note(4 * E + B, p("E4"), 2 * B, 60)
    for pitch in ("A3", "C4", "E4"):
        pad.note(4 * E, p(pitch), 3 * B, 60)
    tracks.append(tail_track(4.5, 100))
    write_midi(out_dir / "jingle_lose.mid", tracks, bpm=100)


if __name__ == "__main__":
    out = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    out.mkdir(parents=True, exist_ok=True)
    compose_main(out)
    compose_win(out)
    compose_lose(out)
    print("wrote", ", ".join(f.name for f in sorted(out.glob("*.mid"))))
