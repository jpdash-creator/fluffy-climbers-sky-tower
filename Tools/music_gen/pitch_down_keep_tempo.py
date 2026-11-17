"""Lowers a loop by one octave and keeps its tempo and length: the 'slow' half-speed file is time-compressed back to the
original length with WSOLA (waveform-similarity overlap-add), treating the loop as circular so the loop point stays seamless.
Usage: python pitch_down_keep_tempo.py <half_speed_in.wav> <original_in.wav> <out.wav>"""
import sys, wave
import numpy as np
from scipy.signal import correlate

def read(p):
    w = wave.open(p); n = w.getnchannels(); fr = w.getframerate()
    a = np.frombuffer(w.readframes(w.getnframes()), dtype='<i2').astype(np.float64).reshape(-1, n)
    return a, fr

slow, fr = read(sys.argv[1]); orig, _ = read(sys.argv[2])
N_in = len(slow); N_out = len(orig)
W = 4096; Hs = W // 2; delta = 1024
K = int(round(N_out / Hs)); Hs_f = N_out / K          # output hop (float) so K frames close the circle exactly
Ha = N_in / K
pad = np.concatenate([slow, slow[:W + 4 * delta + 2 * Hs]], axis=0)
mono = pad.mean(axis=1)
win = 0.5 - 0.5 * np.cos(2 * np.pi * np.arange(W) / W)
out = np.zeros((N_out + 2 * W, slow.shape[1])); prev = 0
for k in range(K):
    p = int(round(k * Ha))
    if k == 0:
        pos = p
    else:
        tmpl = mono[prev + Hs: prev + Hs + W]
        lo = max(0, p - delta)
        region = mono[lo: p + delta + W]
        c = correlate(region, tmpl, mode='valid', method='fft')
        pos = lo + int(np.argmax(c))
    seg = pad[pos: pos + W]
    o = int(round(k * Hs_f))
    out[o:o + W] += seg * win[:, None]
    prev = pos
res = out[:N_out].copy()
tail = out[N_out:N_out + W]
res[:len(tail)] += tail[:len(res)]                     # circular wrap: the loop's tail fades into its head
res = np.clip(np.round(res), -32768, 32767).astype('<i2')
o = wave.open(sys.argv[3], 'wb'); o.setnchannels(slow.shape[1]); o.setsampwidth(2); o.setframerate(fr); o.writeframes(res.tobytes()); o.close()
print('frames', N_out, 'seconds', N_out / fr)
