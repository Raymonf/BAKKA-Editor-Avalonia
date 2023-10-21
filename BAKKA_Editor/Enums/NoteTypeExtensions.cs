namespace BAKKA_Editor.Enums;

internal static class NoteTypeExtensions
{
    public static bool IsTouchNote(this NoteType type)
    {
        return (int) type is >= (int) NoteType.TouchNoBonus and <= (int) NoteType.TouchBonus
               || type == NoteType.TouchBonusFlair;
    }

    public static bool IsSlideNote(this NoteType type)
    {
        return (int) type is >= (int) NoteType.SlideOrangeNoBonus and <= (int) NoteType.SlideGreenBonus
               || type == NoteType.SlideOrangeBonusFlair
               || type == NoteType.SlideGreenBonusFlair;
    }
}