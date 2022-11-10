namespace MeizuLivePhoto.Exceptions;

/// <summary>
/// 加载文件失败
/// <para>例如：文件无法解析</para>
/// </summary>
public class FileLoadingException : Exception
{
    public FileLoadingException(string? message) : base(message)
    {
    }
}