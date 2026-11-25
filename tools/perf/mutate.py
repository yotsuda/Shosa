"""Broken variants of .shosa files, the same ones every time: python mutate.py FOLDER (variants are written beside the files, as NAME.mN.shosa)."""
import glob, os, random, re, sys

folder = sys.argv[1]
made = 0
for path in sorted(glob.glob(os.path.join(folder, '*.shosa'))):
    if re.search(r'\.m\d+\.shosa$', path):
        continue
    text = open(path, encoding='utf-8').read()
    rng = random.Random(os.path.basename(path))
    lines = text.split('\n')
    variants = []

    def body_lines():
        return [i for i, l in enumerate(lines) if l.strip() and not l.strip().startswith('//')]

    for _ in range(3):   # a line gone
        i = rng.choice(body_lines())
        variants.append('\n'.join(lines[:i] + lines[i + 1:]))
    for _ in range(2):   # a line twice
        i = rng.choice(body_lines())
        variants.append('\n'.join(lines[:i + 1] + lines[i:]))
    words = [m for m in re.finditer(r'[A-Za-z][A-Za-z0-9]*', text)]
    for _ in range(4):   # a word that is no word
        m = rng.choice(words)
        variants.append(text[:m.start()] + 'zzz' + text[m.end():])
    for _ in range(3):   # a word for another of the file
        m, other = rng.choice(words), rng.choice(words)
        variants.append(text[:m.start()] + other.group() + text[m.end():])
    bars = [m.start() for m in re.finditer(r'\|', text)]
    for _ in range(2 if bars else 0):   # a bar line gone
        at = rng.choice(bars)
        variants.append(text[:at] + text[at + 1:])
    numbers = [m for m in re.finditer(r'\d+(\.\d+)?', text)]
    for _ in range(3 if numbers else 0):   # another number
        m = rng.choice(numbers)
        variants.append(text[:m.start()] + rng.choice(['0', '7', '1/3', '999']) + text[m.end():])
    for _ in range(4):   # a character gone
        at = rng.randrange(len(text))
        variants.append(text[:at] + text[at + 1:])
    # a move that uses itself, and one that uses itself by way of another
    moves = [m for m in re.finditer(r'\b(move|pose)\s+(\w+)\s*\{[^{}]*?\{', text)]
    if moves:
        m = rng.choice(moves)
        variants.append(text[:m.end()] + ' ' + m.group(2) + ':2 ' + text[m.end():])
    if len(moves) >= 2:
        a, b = rng.sample(moves, 2)
        first, second = sorted([a, b], key=lambda m: m.end())
        changed = text[:second.end()] + ' ' + first.group(2) + ' ' + text[second.end():]
        changed = changed[:first.end()] + ' ' + second.group(2) + ':3 ' + changed[first.end():]
        variants.append(changed)
    # a group done again in odd ways, a bar done again, a tie
    events = [m for m in re.finditer(r'(?<=\{ )[a-z][\w.()]*', text)]
    if events:
        m = rng.choice(events)
        variants.append(text[:m.end()] + ' q q:3@mirror | % | %2 | ' + text[m.end():])
        m = rng.choice(events)
        variants.append(text[:m.start()] + '{ ' + m.group() + ' then } q:0 q@free ' + text[m.end():])
        m = rng.choice(events)
        variants.append(text[:m.end()] + ':1~ | :2 :1 ' + text[m.end():])

    stem = path[:-len('.shosa')]
    for n, variant in enumerate(variants):
        with open(f'{stem}.m{n:02}.shosa', 'w', encoding='utf-8', newline='') as out:
            out.write(variant)
        made += 1
print(made, 'variants')
