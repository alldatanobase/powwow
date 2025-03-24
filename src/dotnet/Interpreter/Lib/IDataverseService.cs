using PowwowLang.Types;

namespace PowwowLang.Lib
{
    public interface IDataverseService
    {
        Value RetrieveMultiple(string fetchXml);
    }
}
