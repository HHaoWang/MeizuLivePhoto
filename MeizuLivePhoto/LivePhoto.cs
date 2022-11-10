// ReSharper disable MemberCanBePrivate.Global

using MeizuLivePhoto.Exceptions;
using MeizuLivePhoto.Utils;

namespace MeizuLivePhoto;

/// <summary>
/// 动态照片，包含了动态照片的相关信息
/// </summary>
public class LivePhoto
{
    /// <summary>
    /// 文件位置
    /// </summary>
    public readonly string FilePath;

    /// <summary>
    /// 文件中包含的文件段
    /// <para>每个文件段都可以作为单独的完整文件存在</para>
    /// </summary>
    public List<SegmentInfo> Segments { get; } = new();

    /// <summary>
    /// 构造函数，打开传入的文件路径对应的文件并解析其内容
    /// </summary>
    /// <param name="filePath">要解析的文件</param>
    public LivePhoto(string filePath)
    {
        FilePath = filePath;
        FileStream fileStream = File.OpenRead(filePath);

        while (fileStream.Position != fileStream.Length)
        {
            // 预读8个字节判断各段文件类型
            byte[] signature = new byte[8];
            _ = fileStream.Read(signature);

            // 回退8字节以供完整读取各文件段
            fileStream.Seek(-8, SeekOrigin.Current);
            switch (signature)
            {
                // JPG格式开始标记
                case [0xFF, 0xD8, ..]:
                    Segments.Add(new SegmentInfo
                        {
                            Type = SegmentInfo.SegmentType.Jpg,
                            StartPosition = fileStream.Position,
                            EndPosition = ReadJpg(fileStream)
                        }
                    );
                    break;
                // PNG格式开始标记
                case [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]:
                    Segments.Add(new SegmentInfo
                        {
                            Type = SegmentInfo.SegmentType.Png,
                            StartPosition = fileStream.Position,
                            EndPosition = ReadPng(fileStream)
                        }
                    );
                    break;
                // ftyp box的header，前四个字节是box长度
                case [.., 0x66, 0x74, 0x79, 0x70]:
                    Segments.Add(new SegmentInfo
                        {
                            Type = SegmentInfo.SegmentType.Mp4,
                            StartPosition = fileStream.Position,
                            EndPosition = ReadMp4(fileStream)
                        }
                    );
                    break;
                // LIVE_CVR，魅族动态图片中一段标记，似乎是用来分隔照片和视频的
                // 所以直接跳过
                case [0x4C, 0x49, 0x56, 0x45, 0x5F, 0x43, 0x56, 0x52]:
                    fileStream.Seek(8, SeekOrigin.Current);
                    break;
                default:
                    throw new FileLoadingException("无法识别的文件段！");
            }
        }

        fileStream.Close();
    }

    /// <summary>
    /// 提取动态照片中的视频保存为新文件
    /// </summary>
    /// <param name="savePath">保存位置</param>
    /// <exception cref="FileNotFoundException">没有视频内容可以提取</exception>
    public void ExtractMp4(string savePath)
    {
        SegmentInfo? segment = Segments.FirstOrDefault(s => s.Type == SegmentInfo.SegmentType.Mp4);
        if (segment is null)
        {
            throw new FileNotFoundException("未找到可以提取的Mp4文件！");
        }

        ExtractSegmentFile(segment, savePath);
    }

    /// <summary>
    /// 提取动态照片中的主图保存为新文件
    /// </summary>
    /// <param name="savePath">保存位置</param>
    /// <exception cref="FileNotFoundException">没有主图内容可以提取</exception>
    public void ExtractJpg(string savePath)
    {
        SegmentInfo? segment = Segments.FirstOrDefault(s => s.Type == SegmentInfo.SegmentType.Jpg);
        if (segment is null)
        {
            throw new FileNotFoundException("未找到可以提取的Jpg文件！");
        }

        ExtractSegmentFile(segment, savePath);
    }

    /// <summary>
    /// 提取动态照片中的全部元素到指定位置并自动命名保存
    /// <para>若给定的保存位置为空或是空白字符串，则自动使用动态图片的路径进行保存</para>
    /// <para>假定给定的savePath为/root/example.jpg，则提取出的全部元素会被依次命名为/root/example-01.Jpg形式，其中的“01”会自动递增，后缀名称则为元素实际内容对应的后缀名</para>
    /// </summary>
    /// <param name="savePath">保存位置</param>
    public void ExtractAll(string? savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
        {
            savePath = FilePath;
        }

        int ext = 1;
        foreach (SegmentInfo segment in Segments)
        {
            string realPath = Path.GetFileNameWithoutExtension(savePath) + $"-{ext:00}." + segment.Type;
            realPath = Path.Combine(Path.GetDirectoryName(savePath)!, realPath);
            ext++;
            ExtractSegmentFile(segment, realPath);
        }
    }

    /// <summary>
    /// 私有方法，供提取文件段使用
    /// </summary>
    /// <param name="segment">文件段</param>
    /// <param name="savePath">保存位置</param>
    /// <exception cref="FileNotFoundException">无法找到动态照片文件</exception>
    private void ExtractSegmentFile(SegmentInfo segment, string savePath)
    {
        FileInfo mp4File = new(FilePath);
        if (!mp4File.Exists)
        {
            throw new FileNotFoundException("文件不存在！");
        }

        FileUtils.ExtractFile(mp4File, segment.StartPosition, segment.EndPosition, savePath);
    }

    /// <summary>
    /// 读取jpg文件流
    /// </summary>
    /// <param name="fileStream">待读取的流，该流当前游标应该位于jpg开始处</param>
    /// <returns>jpg在流中的结束位置</returns>
    /// <exception cref="FileLoadingException">解析出现问题</exception>
    private static long ReadJpg(Stream fileStream)
    {
        byte[] tag = new byte[2];
        //读取文件头
        int read = fileStream.Read(tag);
        if (read < 2 || tag is not [0xFF, 0xD8])
        {
            throw new FileLoadingException("不是有效的jpg图片！");
        }

        // 图片内容数据部分是否开始
        bool imageStreamStart = false;

        //读取段
        byte[] current = new byte[1];
        current[0] = 0x00;
        byte pre = current[0];
        while (true)
        {
            // 若开始读取图像数据，则一直读取直到Jpg结尾
            if (imageStreamStart)
            {
                pre = current[0];
                int gotByteLength = fileStream.Read(current);

                if (gotByteLength < 1)
                {
                    throw new FileLoadingException("文件已读完，但未发现Jpg结尾！");
                }

                int maker = (pre << 8) + current[0];

                // FFD9: Image File End Mark
                // 结束读取
                if (maker == 0xFFD9)
                {
                    break;
                }

                continue;
            }

            read = fileStream.Read(tag);
            if (read < 2)
            {
                throw new FileLoadingException("解析段标签失败，不是有效的jpg图片！");
            }

            // FFDA: Image Stream Start
            if (tag is [0xFF, 0xDA])
            {
                imageStreamStart = true;
            }

            byte[] segmentDataSize = new byte[2];
            read = fileStream.Read(segmentDataSize);
            if (read < 2)
            {
                throw new FileLoadingException("解析段长失败，不是有效的jpg图片！");
            }
            // 读取到段长后直接跳到下一段，段大小包含了段大小的两字节
            int segmentDataSizeValue = (segmentDataSize[0] << 8) + segmentDataSize[1] - 2;
            fileStream.Seek(segmentDataSizeValue, SeekOrigin.Current);
        }

        return fileStream.Position - 1;
    }

    /// <summary>
    /// 读取png文件流
    /// </summary>
    /// <param name="fileStream">待读取的流</param>
    /// <returns>png在流中结束的位置</returns>
    /// <exception cref="FileLoadingException">文件解析失败</exception>
    private static long ReadPng(Stream fileStream)
    {
        byte[] signature = new byte[8];
        int gotBytesLength = fileStream.Read(signature);
        if (gotBytesLength < 8 || signature is not [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        {
            throw new FileLoadingException("不是合法PNG图片！");
        }

        while (true)
        {
            // PNG End Mark有12字节，chunk头部固定数据8字节，取较大的长度
            byte[] startOfChunk = new byte[12];
            gotBytesLength = fileStream.Read(startOfChunk);
            if (gotBytesLength < 12)
            {
                throw new FileLoadingException("PNG格式有误！");
            }

            // PNG End
            if (startOfChunk is [0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82])
            {
                break;
            }

            // PNG文件中默认大端对齐，若本机是小端对齐则需要转换
            byte[] reversedBytes = startOfChunk[..4];
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(reversedBytes);
            }

            uint chunkDataLength = BitConverter.ToUInt32(reversedBytes);

            // 为了判断是否到达PNG文件尾预读取了12字节，正常chunk中前8字节为有用信息
            // 后4字节为数据开始部分，每个chunk末尾又有4个字节CRC校验码
            // 故可以抵消，直接跳chunkDataLength个字节即可
            fileStream.Seek(chunkDataLength, SeekOrigin.Current);
        }

        return fileStream.Position - 1;
    }

    /// <summary>
    /// 读取mp4流
    /// <para>mp4规范未规定结束标记，因此无法判断mp4部分是否结束，猜测这也是魅族把视频部分放在整个文件最后的原因，因此检测到mp4头后直接判断剩下的部分均为mp4数据</para>
    /// </summary>
    /// <param name="fileStream">待读取的流</param>
    /// <returns>mp4在流中结束的位置</returns>
    private static long ReadMp4(Stream fileStream)
    {
        fileStream.Seek(0, SeekOrigin.End);
        return fileStream.Position - 1;
    }

    /// <summary>
    /// 文件段信息
    /// </summary>
    public class SegmentInfo
    {
        /// <summary>
        /// 文件段类型
        /// </summary>
        public SegmentType Type { get; init; }

        /// <summary>
        /// 文件段在文件中的起始位置
        /// </summary>
        public long StartPosition { get; init; }

        /// <summary>
        /// 文件段在文件中的结束位置
        /// </summary>
        public long EndPosition { get; init; }

        /// <summary>
        /// 文件段长度
        /// </summary>
        public long Length => EndPosition - StartPosition + 1;

        /// <summary>
        /// 文件段类型
        /// </summary>
        public enum SegmentType
        {
            Jpg,
            Png,
            Mp4,
        }
    }
}