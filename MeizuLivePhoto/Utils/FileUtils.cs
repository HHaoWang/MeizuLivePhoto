using MeizuLivePhoto.Exceptions;

namespace MeizuLivePhoto.Utils;

/// <summary>
/// 文件操作工具类
/// </summary>
public static class FileUtils
{
    /// <summary>
    /// 以二进制形式提取文件片段至新文件中
    /// <para>提取的片段是[startPos,endPos]范围中的（即包括两端）</para>
    /// </summary>
    /// <param name="file">被提取的文件信息</param>
    /// <param name="startPos">开始提取的位置</param>
    /// <param name="endPos">结束提取的位置</param>
    /// <param name="targetPath">保存位置</param>
    /// <exception cref="Exception"></exception>
    public static void ExtractFile(FileInfo file, long startPos, long endPos, string targetPath)
    {
        FileStream sourceStream = file.OpenRead();
        FileStream outputStream = File.Create(targetPath);

        if (endPos > sourceStream.Length)
        {
            endPos = (int) sourceStream.Length;
        }

        byte[] seg = new byte[endPos - startPos + 1];

        sourceStream.Seek(startPos, SeekOrigin.Begin);
        int readLength = sourceStream.Read(seg);
        if (readLength < endPos - startPos + 1)
        {
            throw new FileLoadingException("读取文件失败！文件长度不足！");
        }

        outputStream.Write(seg);

        sourceStream.Close();
        outputStream.Close();
    }
}