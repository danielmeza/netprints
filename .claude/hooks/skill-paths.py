#!/usr/bin/env python3
"""PreToolUse hook: the first time an agent edits a file that matches a skill's `paths:` globs,
deny the edit once and name the skills to load. Any error exits 0 silently."""
import json
import os
import re
import sys
import tempfile


def find_root(file_path):
    d = os.path.dirname(os.path.abspath(file_path))
    while True:
        if os.path.isdir(os.path.join(d, ".claude", "skills")):
            return d
        parent = os.path.dirname(d)
        if parent == d:
            return None
        d = parent


def parse_paths(text):
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return []
    globs = []
    in_paths = False
    for line in lines[1:]:
        if line.strip() == "---":
            break
        if in_paths:
            m = re.match(r"^\s+-\s*(.+?)\s*$", line)
            if m:
                globs.append(m.group(1).strip("'\""))
                continue
            in_paths = False
        m = re.match(r"^paths:\s*(.*)$", line)
        if m:
            rest = m.group(1).strip()
            if rest.startswith("["):
                globs += [g.strip().strip("'\"") for g in rest.strip("[]").split(",") if g.strip()]
            elif rest:
                globs.append(rest.strip("'\""))
            else:
                in_paths = True
    return globs


def glob_to_regex(glob):
    out = []
    i = 0
    while i < len(glob):
        c = glob[i]
        if glob.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif glob.startswith("**", i):
            out.append(".*")
            i += 2
        elif c == "*":
            out.append("[^/]*")
            i += 1
        elif c == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(c))
            i += 1
    return re.compile("^" + "".join(out) + "$")


def matching_skills(root, file_path):
    rel = os.path.relpath(os.path.abspath(file_path), root).replace(os.sep, "/")
    skills_dir = os.path.join(root, ".claude", "skills")
    names = []
    for name in sorted(os.listdir(skills_dir)):
        skill_file = os.path.join(skills_dir, name, "SKILL.md")
        if not os.path.isfile(skill_file):
            continue
        with open(skill_file, encoding="utf-8") as f:
            globs = parse_paths(f.read())
        if any(glob_to_regex(g).match(rel) for g in globs):
            names.append(name)
    return names


def first_time(session_id, agent_id):
    key = re.sub(r"[^A-Za-z0-9_.-]", "_", f"{session_id}-{agent_id or 'main'}")
    state_dir = os.path.join(tempfile.gettempdir(), "skill-paths-hook")
    os.makedirs(state_dir, exist_ok=True)
    try:
        fd = os.open(os.path.join(state_dir, key), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        return False
    os.close(fd)
    return True


def main():
    data = json.load(sys.stdin)
    file_path = (data.get("tool_input") or {}).get("file_path")
    if not file_path:
        return
    root = find_root(file_path)
    if root is None:
        return
    names = matching_skills(root, file_path)
    if not names:
        return
    if not first_time(data.get("session_id") or "nosession", data.get("agent_id")):
        return
    reason = (
        f"Project rule: before editing {os.path.basename(file_path)}, load these skills with the Skill tool: "
        f"{', '.join(names)}. Then retry this edit."
    )
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
    sys.exit(0)
