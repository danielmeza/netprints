#!/usr/bin/env python3
"""PreToolUse hook (matcher Bash): deny a shell command that writes into a file of the repo working tree, so edits go
through the Edit/Write tools (which the skill-paths hook guards). Conservative: a command it cannot resolve is allowed.
Any error exits 0 silently."""
import json
import os
import re
import sys

MESSAGE = "Edit repo files with the Edit/Write tools, not the shell."
BUILD_DIRS = {"bin", "obj", "TestResults"}
WRAPPERS = {"rtk", "proxy", "sudo", "env", "time", "nice", "nohup", "command", "exec"}
SHELLS = {"bash", "sh", "zsh", "dash"}
PYTHON = re.compile(r"^python[0-9.]*$")
HEREDOC = re.compile(r"(?<!<)<<(-?)\s*(['\"]?)([A-Za-z_]\w*)\2")
PY_WRITES = re.compile(
    r"open\s*\(\s*[^,()]+,\s*(?:mode\s*=\s*)?['\"][rwxabt+]*[wax][rwxabt+]*['\"]"
    r"|\.open\s*\(\s*['\"][rwxabt+]*[wax][rwxabt+]*['\"]"
    r"|\.write_text\s*\(|\.write_bytes\s*\("
)
PY_SCRATCH = re.compile(r"/tmp/|/dev/null|tempfile")


class Segment:
    def __init__(self, words, redirs):
        self.words = words
        self.redirs = redirs


def split_heredocs(command):
    bodies, out, pending = [], [], []
    for line in command.split("\n"):
        if pending:
            strip, delim, idx = pending[0]
            if (line.lstrip("\t") if strip else line) == delim:
                pending.pop(0)
            else:
                bodies[idx] += line + "\n"
            continue
        out.append(line)
        for m in HEREDOC.finditer(line):
            bodies.append("")
            pending.append((m.group(1) == "-", m.group(3), len(bodies) - 1))
    return "\n".join(out), bodies


def scan(text):
    """Split a command into simple commands: words, plus the file targets of `>`-style redirections and the
    here-strings (`<<<`)."""
    segments, strings = [], []
    state = {"words": [], "redirs": [], "buf": [], "started": False, "pending": None}

    def flush():
        if not state["started"]:
            return
        word = "".join(state["buf"])
        state["buf"], state["started"] = [], False
        kind, state["pending"] = state["pending"], None
        if kind == "file":
            state["redirs"].append(word)
        elif kind == "string":
            strings.append(word)
        elif kind is None:
            state["words"].append(word)

    def end_segment():
        flush()
        state["pending"] = None
        if state["words"] or state["redirs"]:
            segments.append(Segment(state["words"], state["redirs"]))
        state["words"], state["redirs"] = [], []

    buf = state["buf"]
    i, n, depth = 0, len(text), 0
    while i < n:
        buf = state["buf"]
        c = text[i]
        if depth:
            buf.append(c)
            depth += (c == "(") - (c == ")")
            i += 1
        elif c == "\\" and i + 1 < n:
            buf.append(text[i + 1])
            state["started"] = True
            i += 2
        elif c == "'":
            j = text.find("'", i + 1)
            j = n if j < 0 else j
            buf.append(text[i + 1:j])
            state["started"] = True
            i = j + 1
        elif c == '"':
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\" and i + 1 < n:
                    i += 1
                buf.append(text[i])
                i += 1
            state["started"] = True
            i += 1
        elif c == "$" and text[i + 1:i + 2] == "(":
            buf.append("$(")
            state["started"] = True
            depth = 1
            i += 2
        elif c == "`":
            j = text.find("`", i + 1)
            j = n - 1 if j < 0 else j
            buf.append(text[i:j + 1])
            state["started"] = True
            i = j + 1
        elif c == "#" and not state["started"]:
            while i < n and text[i] != "\n":
                i += 1
        elif c in " \t":
            flush()
            i += 1
        elif c in ";\n":
            end_segment()
            i += 1
        elif c == "|":
            end_segment()
            i += 2 if text[i + 1:i + 2] in ("|", "&") else 1
        elif c == "&" and text[i + 1:i + 2] == ">":
            flush()
            i += 3 if text[i + 2:i + 3] == ">" else 2
            state["pending"] = "file"
        elif c == "&":
            end_segment()
            i += 2 if text[i + 1:i + 2] == "&" else 1
        elif c == ">":
            if state["started"] and "".join(buf).isdigit():
                state["buf"], state["started"] = [], False
            else:
                flush()
            i += 1
            if text[i:i + 1] in (">", "|"):
                i += 1
            if text[i:i + 1] == "&":
                i += 1
                while i < n and (text[i].isdigit() or text[i] == "-"):
                    i += 1
            else:
                state["pending"] = "file"
        elif c == "<":
            flush()
            if text.startswith("<<<", i):
                i += 3
                state["pending"] = "string"
            elif text.startswith("<<", i):
                i += 3 if text[i + 2:i + 3] == "-" else 2
                state["pending"] = "skip"
            else:
                i += 1
                state["pending"] = "skip"
        else:
            buf.append(c)
            state["started"] = True
            i += 1
    end_segment()
    return segments, strings


def expand(path, env):
    if path.startswith("~"):
        path = (env.get("HOME") or "~") + path[1:]
    unresolved = False

    def sub(m):
        nonlocal unresolved
        value = env.get(m.group(1) or m.group(2))
        if value is None:
            unresolved = True
            return ""
        return value

    path = re.sub(r"\$(?:\{(\w+)\}|(\w+))", sub, path)
    if unresolved or "$" in path or "`" in path or "*" in path or not path:
        return None
    return path


def flagged(path, ctx):
    p = expand(path, ctx["env"])
    if p is None:
        return False
    full = os.path.normpath(os.path.join(ctx["cwd"], p))
    for root in ctx["roots"]:
        rel = os.path.relpath(full, root)
        if rel == "." or rel == ".." or rel.startswith(".." + os.sep):
            continue
        return not BUILD_DIRS.intersection(rel.split(os.sep)[:-1])
    return False


def positionals(args, takes_value, script_flags=()):
    """Non-flag arguments; the first one is the script when no flag in `script_flags` supplied it."""
    out, scripted, i = [], False, 0
    while i < len(args):
        a = args[i]
        if a in takes_value:
            scripted = scripted or a in script_flags
            i += 2
            continue
        if not a.startswith("-") or a == "-":
            out.append(a)
        i += 1
    return out, scripted


def python_writes(segment_args, bodies):
    code = []
    for idx, a in enumerate(segment_args):
        if a == "-c" and idx + 1 < len(segment_args):
            code.append(segment_args[idx + 1])
    reads_stdin = not code and "-m" not in segment_args and not any(not a.startswith("-") for a in segment_args)
    if reads_stdin:
        code += bodies
    text = "\n".join(code)
    return bool(PY_WRITES.search(text)) and not PY_SCRATCH.search(text)


def check_words(words, ctx, depth):
    while words and (re.match(r"^[A-Za-z_]\w*=", words[0]) or os.path.basename(words[0]) in WRAPPERS):
        wrapper = os.path.basename(words[0]) in WRAPPERS
        words = words[1:]
        while wrapper and words and words[0].startswith("-"):
            words = words[1:]
    if not words:
        return False
    name = os.path.basename(words[0].lstrip("({"))
    args = words[1:]
    if name == "bg-run.sh" and "--" in args:
        return check_words(args[args.index("--") + 1:], ctx, depth)
    if name in SHELLS and depth < 3:
        for idx, a in enumerate(args):
            if re.match(r"^-[a-z]*c[a-z]*$", a) and idx + 1 < len(args):
                return check(args[idx + 1], ctx, depth + 1)
        return False
    if name == "sed":
        if not any(a.startswith("--in-place") or re.match(r"^-[a-zA-Z]*i", a) for a in args if not a.startswith("--") or a.startswith("--in-place")):
            return False
        pos, scripted = positionals(args, {"-e", "-f", "--expression", "--file"}, {"-e", "-f", "--expression", "--file"})
        return any(flagged(f, ctx) for f in (pos if scripted else pos[1:]))
    if name == "perl":
        if not any(re.match(r"^-[0-9lnpaEwWcsS]*i", a) for a in args if a.startswith("-")):
            return False
        pos, scripted = positionals(args, {"-e", "-E"}, {"-e", "-E"})
        return any(flagged(f, ctx) for f in (pos if scripted else pos[1:]))
    if PYTHON.match(name):
        return python_writes(args, ctx["bodies"])
    if name == "tee":
        return any(flagged(f, ctx) for f in positionals(args, set())[0])
    if name == "truncate":
        return any(flagged(f, ctx) for f in positionals(args, {"-s", "--size", "-r", "--reference"})[0])
    if name == "dd":
        return any(a.startswith("of=") and flagged(a[3:], ctx) for a in args)
    return False


def check(command, ctx, depth=0):
    text, bodies = split_heredocs(command)
    segments, strings = scan(text)
    ctx = dict(ctx, bodies=ctx.get("bodies", []) + bodies + strings)
    for seg in segments:
        words = seg.words
        if words and words[0] == "cd" and len(words) > 1:
            target = expand(words[1], ctx["env"])
            if target is not None:
                ctx["cwd"] = os.path.normpath(os.path.join(ctx["cwd"], target))
            continue
        if words and os.path.basename(words[0]) == "git":
            continue
        if any(flagged(r, ctx) for r in seg.redirs) or check_words(words, ctx, depth):
            return True
    return False


def decide(command, cwd, roots, env):
    """Return the deny message for a command that writes into a repo file, else None."""
    ctx = {"cwd": cwd, "roots": roots, "env": env}
    return MESSAGE if check(command, ctx) else None


def main():
    data = json.load(sys.stdin)
    command = (data.get("tool_input") or {}).get("command")
    if not command:
        return
    root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    reason = decide(command, data.get("cwd") or os.getcwd(), [root], dict(os.environ))
    if reason:
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": reason,
        }}))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        pass
