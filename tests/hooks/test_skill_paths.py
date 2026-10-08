"""Run: python3 -m unittest discover -s tests/hooks -v"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
import uuid

HOOK = os.path.join(os.path.dirname(__file__), "..", "..", ".claude", "hooks", "skill-paths.py")
SKILL = '---\nname: s\npaths:\n  - "**/*.axaml"\n  - "**/NetPrints.Editor/**/*.cs"\n---\nbody\n'


def run(stdin_text):
    p = subprocess.run([sys.executable, HOOK], input=stdin_text, capture_output=True, text=True)
    return p.returncode, p.stdout.strip()


class SkillPathsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.session = str(uuid.uuid4())
        self.root = self.tmp.name
        self.make_skills(self.root, "avalonia-xaml")

    def make_skills(self, root, name):
        d = os.path.join(root, ".claude", "skills", name)
        os.makedirs(d)
        with open(os.path.join(d, "SKILL.md"), "w") as f:
            f.write(SKILL)

    def call(self, rel, agent=None, root=None):
        payload = {"session_id": self.session, "tool_input": {"file_path": os.path.join(root or self.root, rel)}}
        if agent:
            payload["agent_id"] = agent
        return run(json.dumps(payload))

    def test_nested_axaml_denies_once_per_agent(self):
        code, out = self.call("src/A/B/View.axaml", agent="a1")
        self.assertEqual(code, 0)
        decision = json.loads(out)["hookSpecificOutput"]
        self.assertEqual(decision["permissionDecision"], "deny")
        self.assertIn("avalonia-xaml", decision["permissionDecisionReason"])
        self.assertEqual(self.call("src/A/B/View.axaml", agent="a1"), (0, ""))
        self.assertIn("deny", self.call("src/A/Other.axaml", agent="a2")[1])

    def test_main_session_without_agent_id(self):
        self.assertIn("deny", self.call("View.axaml")[1])
        self.assertEqual(self.call("View.axaml"), (0, ""))

    def test_editor_cs_matches_and_core_does_not(self):
        self.assertIn("deny", self.call("src/NetPrints.Editor/Shell/X.cs", agent="b")[1])
        self.assertEqual(self.call("src/NetPrints.Core/X.cs", agent="c"), (0, ""))

    def test_worktree_uses_its_own_skills(self):
        wt = os.path.join(self.root, ".worktrees", "w1")
        self.make_skills(wt, "wt-skill")
        out = self.call("src/V.axaml", agent="d", root=wt)[1]
        self.assertIn("wt-skill", out)
        self.assertNotIn("avalonia-xaml", out)

    def test_malformed_json_is_silent(self):
        self.assertEqual(run("{not json"), (0, ""))
        self.assertEqual(run(""), (0, ""))


if __name__ == "__main__":
    unittest.main()
