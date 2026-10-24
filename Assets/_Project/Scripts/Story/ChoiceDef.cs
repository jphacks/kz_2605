namespace JPHACKS2026
{
    /// <summary>分岐1つ分の定義。選択肢ごとに「どの行動で成立するか」をBranchTriggerで持つ。</summary>
    public class ChoiceDef
    {
        public string question;
        public float timeLimit = 20f;
        public Option[] options;

        public class Option
        {
            public string label;
            public string hint;
            public BranchTrigger[] triggers;

            public Option(string label, string hint, params BranchTrigger[] triggers)
            {
                this.label = label;
                this.hint = hint;
                this.triggers = triggers;
            }
        }
    }
}
