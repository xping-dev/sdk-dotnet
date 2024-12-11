namespace Xping.Sdk.Validations.Content.Html.Internals;

internal interface IIterator<out T>
{
    void First();
    void Last();
    void Nth(int index);
    int Cursor { get; }
    T? Current();
    bool IsAdvanced { get; }
    int Count { get; }
}
