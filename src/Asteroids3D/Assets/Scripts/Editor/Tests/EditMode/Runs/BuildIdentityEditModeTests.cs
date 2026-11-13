using System;
using Game.Runs;
using NUnit.Framework;

namespace Tests.EditMode.Runs
{
    /// <summary>The stamped build-identity file round-trips, including its label, and refuses a file with no commit; a file with no label reads as empty; the label rule accepts a short name and refuses one that cannot be a file name. Git's porcelain-v2 status yields the commit and reads any tracked change or untracked file as dirty.</summary>
    [Category("Bootstrap")]
    public class BuildIdentityEditModeTests
    {
        private const string Commit = "8ef7fadc0b738abeb380a605e0344f828aeeac32";
        private const string Headers = "# branch.oid " + Commit + "\n# branch.head main\n# branch.upstream origin/main\n# branch.ab +0 -0\n";

        [Test]
        public void StampedFile_RoundTrips()
        {
            var parsed = BuildIdentity.Parse(new BuildIdentity { commit = Commit, dirty = true }.ToJson());

            Assert.AreEqual(Commit, parsed.commit);
            Assert.IsTrue(parsed.dirty);
        }

        [Test]
        public void Label_RoundTrips()
        {
            var parsed = BuildIdentity.Parse(new BuildIdentity
            {
                commit = Commit,
                dirty = false,
                label = "friends-1",
            }.ToJson());

            Assert.AreEqual("friends-1", parsed.label);
            Assert.AreEqual(Commit, parsed.commit);
            Assert.IsFalse(parsed.dirty);
        }

        [Test]
        public void StampedFile_WithoutALabel_ParsesAsEmpty()
        {
            var parsed = BuildIdentity.Parse("{\"commit\":\"" + Commit + "\",\"dirty\":true}");

            Assert.AreEqual("", parsed.label);
            Assert.AreEqual(Commit, parsed.commit);
            Assert.IsTrue(parsed.dirty);
        }

        [Test]
        public void LabelRule_AcceptsAShortName_AndRefusesOneThatCannotBeAFileName()
        {
            Assert.IsTrue(BuildIdentity.TryAcceptLabel("friends-1", out var failure), failure);
            Assert.IsTrue(BuildIdentity.TryAcceptLabel("itch-1.0", out failure), failure);

            Assert.IsFalse(BuildIdentity.TryAcceptLabel("", out failure), "empty");
            StringAssert.Contains("1 to 32", failure);
            Assert.IsFalse(BuildIdentity.TryAcceptLabel(" ", out _), "space");
            Assert.IsFalse(BuildIdentity.TryAcceptLabel("a/b", out _), "path separator");
            Assert.IsFalse(BuildIdentity.TryAcceptLabel(new string('a', 33), out _), "33 characters");
        }

        [Test]
        public void StampedFile_WithoutACommit_IsRefused()
        {
            Assert.Throws<FormatException>(() => BuildIdentity.Parse("{\"dirty\":false}"));
        }

        [Test]
        public void GitStatus_HeadersOnly_IsClean()
        {
            var identity = BuildIdentity.ParseGitStatus(Headers);

            Assert.AreEqual(Commit, identity.commit);
            Assert.IsFalse(identity.dirty);
            Assert.AreEqual("", identity.label);
        }

        [TestCase("1 .M N... 100644 100644 100644 1111111 2222222 Assets/Scripts/Game/GameHost.cs\n", TestName = "GitStatus_TrackedChange_IsDirty")]
        [TestCase("? Assets/Scratch.cs\n", TestName = "GitStatus_UntrackedFile_IsDirty")]
        public void GitStatus_AnyEntry_IsDirty(string entry)
        {
            var identity = BuildIdentity.ParseGitStatus(Headers + entry);

            Assert.AreEqual(Commit, identity.commit);
            Assert.IsTrue(identity.dirty);
        }

        [Test]
        public void GitStatus_WithoutACommit_IsRefused()
        {
            Assert.Throws<FormatException>(() => BuildIdentity.ParseGitStatus("# branch.head main\n"));
        }
    }
}
