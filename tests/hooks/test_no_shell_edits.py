"""Run: python3 -m unittest discover -s tests/hooks -v"""
import importlib.util
import json
import os
import subprocess
import sys
import unittest

HOOK = os.path.join(os.path.dirname(__file__), "..", "..", ".claude", "hooks", "no-shell-edits.py")
spec = importlib.util.spec_from_file_location("no_shell_edits", HOOK)
hook = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hook)

ROOT = "/work/repo"
ENV = {"CLAUDE_PROJECT_DIR": ROOT, "TMPDIR": "/tmp/agent", "HOME": "/users/dev"}

ALLOW = [
    "dotnet test --no-build --no-progress --no-ansi > /tmp/x",
    "dotnet build -v q -tl:off --nologo 2>&1 | tail -20",
    "git -C /work/repo diff --stat",
    "git -C /work/repo apply fix.patch",
    "git -C /work/repo checkout -- src/A.cs",
    "git -C /work/repo stash",
    "git -C /work/repo show HEAD:src/A.cs > /tmp/old.cs",
    'grep -rn "RetargetCallers" src | head',
    "~/.claude/scripts/bg-run.sh netprints-test 570 -- dotnet test --no-build --no-progress --no-ansi",
    "sed -n '10,20p' src/A.cs",
    "dotnet test --no-build > /work/repo/tests/X/TestResults/out.txt",
    "dotnet build 2>&1 > /work/repo/src/A/obj/log.txt",
    "echo hi > $TMPDIR/x.txt",
    "cat src/A.cs > /dev/null 2>&1",
    "python3 -m unittest discover -s tests/hooks",
    'python3 -c "print(1)"',
    "python3 - <<'EOF'\nimport json\nprint(json.dumps({}))\nEOF",
    "python3 -c \"open('/tmp/o.txt','w').write('x')\"",
    "python3 -c \"print(open('data.txt').read())\"",
    "perl -ne 'print if /x/' src/A.cs",
    "perl -MFile::Find -e 'print 1'",
    "sed 's/a/b/' src/A.cs > /tmp/out.cs",
    "sed -i 's/a/b/' /tmp/x.txt",
    "gh pr view 12 --json comments --jq '.comments[-3:]'",
    "cat <<'EOF' > /tmp/body.md\ntext\nEOF",
    "cd /work/repo/src && grep -rn foo . | head",
    "tee /tmp/a.txt < src/A.cs",
    "dd if=src/A.cs of=/tmp/a.bak",
    'echo x > "$UNSET/file"',
    "truncate -s 0 /tmp/a.txt",
    "gh pr comment 12 --body-file notes.md",
    "echo x > /dev/null 2>&1",
    "OUT=/tmp/agent; cat >> $OUT/notes.md <<EOF\nx\nEOF",
    "cat >> $TMPDIR/notes.md <<'EOF'\nx\nEOF",
    "bash <<EOF\necho x > /tmp/y\nEOF",
    "git -C /work/repo log --oneline | head -5",
    "echo x | tee -a /tmp/agent/a.txt",
    "echo x | sponge /tmp/a.txt",
    "echo x > /users/dev/notes.md",
]

DENY = [
    "sed -i 's/a/b/' src/A.cs",
    "sed -i.bak -e 's/a/b/' src/A.cs",
    "sed -i -e 's/a/b/' -e 's/c/d/' src/A.cs src/B.cs",
    "perl -pi -e 's/a/b/' src/A.cs",
    "perl -i.bak -pe 's/a/b/' src/A.cs",
    "FOO=1 perl -pi -e 's/a/b/' src/A.cs",
    "python3 -c \"open('src/A.cs','w').write('x')\"",
    "python3 - <<'EOF'\nfrom pathlib import Path\nPath('src/A.cs').write_text('x')\nEOF",
    "python -c \"from pathlib import Path; Path('a.txt').write_bytes(b'x')\"",
    'python3 <<EOF\nwith open("tasks.md", "a") as f:\n    f.write("x")\nEOF',
    "echo x >> src/A.cs",
    "echo x > specs/005-editor-shell/tasks.md",
    "cat > src/New.cs <<'EOF'\nclass A {}\nEOF",
    "printf 'x' | tee src/A.cs",
    "tee -a AGENTS.md < /tmp/x",
    "truncate -s 0 src/A.cs",
    "dd if=/tmp/x of=src/A.cs",
    "cd src && sed -i 's/a/b/' A.cs",
    "echo x > $CLAUDE_PROJECT_DIR/README.md",
    "~/.claude/scripts/bg-run.sh x 570 -- sed -i 's/a/b/' src/A.cs",
    'bash -c "echo x > src/A.cs"',
    "dotnet test > src/out.txt",
    "echo x &> src/log.txt",
    "git -C /work/repo diff && echo x > /work/repo/notes.md",
    "rtk sed -i 's/a/b/' src/A.cs",
    "echo x > /work/repo/src/bin_notes/A.cs",
    "cat a.txt > ./b.txt",
    "echo x 2> src/err.txt",
    "python3 <<<\"open('a.cs', 'w').close()\"",
    "sed -i 's/a/b/' /work/repo/src/A.cs",
    "cat >> specs/x.md <<'EOF'\nhi\nEOF",
    "cat <<'EOF' >> specs/x.md\nhi\nEOF",
    "cat >>specs/x.md <<EOF\nhi\nEOF",
    "echo x | tee --append notes.md",
    "echo x >| notes.md",
    "{ echo x; } > notes.md",
    "git -C /work/repo show HEAD:a > src/A.cs",
    "git -C /work/repo diff >> notes.md",
    "REPO=/work/repo; cat >> $REPO/notes.md <<EOF\nx\nEOF",
    "export REPO=/work/repo && echo x > $REPO/notes.md",
    "bash <<EOF\necho x > notes.md\nEOF",
    "echo x | sponge notes.md",
]


class NoShellEditsTests(unittest.TestCase):
    def test_allow(self):
        self.assertGreaterEqual(len(ALLOW), 15)
        for command in ALLOW:
            with self.subTest(command=command):
                self.assertIsNone(hook.decide(command, ROOT, [ROOT], ENV))

    def test_deny(self):
        self.assertGreaterEqual(len(DENY), 15)
        for command in DENY:
            with self.subTest(command=command):
                self.assertEqual(hook.MESSAGE, hook.decide(command, ROOT, [ROOT], ENV))

    def test_message(self):
        self.assertEqual("Edit repo files with the Edit/Write tools, not the shell.", hook.MESSAGE)

    def run_hook(self, command):
        repo = os.path.abspath(os.path.join(os.path.dirname(HOOK), "..", ".."))
        payload = {"cwd": repo, "tool_input": {"command": command}}
        p = subprocess.run([sys.executable, HOOK], input=json.dumps(payload), capture_output=True, text=True)
        return p.returncode, p.stdout.strip()

    def test_script_denies_in_the_repo(self):
        code, out = self.run_hook("sed -i 's/a/b/' AGENTS.md")
        self.assertEqual(0, code)
        decision = json.loads(out)["hookSpecificOutput"]
        self.assertEqual("deny", decision["permissionDecision"])
        self.assertEqual(hook.MESSAGE, decision["permissionDecisionReason"])

    def test_script_allows_a_read(self):
        self.assertEqual((0, ""), self.run_hook("grep -n x AGENTS.md | head"))

    def test_script_ignores_bad_input(self):
        p = subprocess.run([sys.executable, HOOK], input="not json", capture_output=True, text=True)
        self.assertEqual((0, ""), (p.returncode, p.stdout.strip()))


if __name__ == "__main__":
    unittest.main()
