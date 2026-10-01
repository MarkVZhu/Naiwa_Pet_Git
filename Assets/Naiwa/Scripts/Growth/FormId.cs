namespace Naiwa.Growth
{
    public enum FormId
    {
        Egg = 0,
        Small = 1,
        Big = 2,
    }

    public static class FormIdExtensions
    {
        public const FormId First = FormId.Egg;
        public const FormId Last = FormId.Big;

        public static string DisplayName(this FormId form)
        {
            switch (form)
            {
                case FormId.Egg: return "奶蛋";
                case FormId.Small: return "小奶蛙";
                case FormId.Big: return "大奶蛙";
                default: return form.ToString();
            }
        }

        public static bool HasNext(this FormId form) => form < Last;

        public static FormId Next(this FormId form) => form < Last ? form + 1 : form;
    }
}
