namespace Game.RLHarness
{
    /// <summary>The single-session AssetBundle contract between the editor convert step (<c>RLEvalModelConvert</c> writes it) and the player eval boot (<c>EvalPlayerBoot</c> loads it): one bundle per session, candidate + optional checkpoint-opponent ModelAssets under these fixed names.</summary>
    public static class EvalModelBundle
    {
        public const string FileName = "eval-models.bundle";
        public const string CandidateAsset = "candidate";
        public const string OpponentAsset = "opponent";
    }
}
