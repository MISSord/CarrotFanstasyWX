using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class FileDownloadHandler : DownloadHandlerScript
{
    private FileStream fileStream;
    private string tempFilePath;
    private string finalFilePath;
    private long expectedContentLength; // 预期总字节数
    private long totalReceivedBytes; // 已接收的总字节数

    public FileDownloadHandler(string tempPath, string finalPath, byte[] buffer) : base(buffer)
    {
        this.tempFilePath = tempPath;
        this.finalFilePath = finalPath;

        // 以追加模式打开文件流，支持断点续传
        bool fileExists = File.Exists(tempFilePath);
        fileStream = new FileStream(tempFilePath, fileExists ? FileMode.Append : FileMode.Create, FileAccess.Write);

        // 如果文件已存在，则已接收的字节数从当前文件长度开始计算
        totalReceivedBytes = fileExists ? fileStream.Length : 0;

        Debug.Log($"准备下载，临时文件: {tempFilePath}，初始大小: {totalReceivedBytes} 字节");
    }

    // 接收到数据长度头信息时调用
    protected override void ReceiveContentLengthHeader(ulong contentLength)
    {
        Debug.Log($"服务器返回文件总大小: {contentLength} 字节");
        expectedContentLength = (long)contentLength + totalReceivedBytes; // 加上已下载的，得到完整文件大小
    }

    // 接收到数据时调用，dataLength是本次接收的数据长度
    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || data.Length == 0)
        {
            Debug.Log("下载数据为空");
            return false;
        }

        // 将数据写入文件
        fileStream.Write(data, 0, dataLength);
        totalReceivedBytes += dataLength;

        return true;
    }

    // 获取下载进度（0.0 到 1.0）
    public float GetProgress()
    {
        if (expectedContentLength <= 0)
            return 0f;

        return (float)totalReceivedBytes / expectedContentLength;
    }

    // 下载完成时调用
    protected override void CompleteContent()
    {
        Debug.Log("下载完成，开始清理资源");
        CloseFileStream();

        // 将临时文件重命名为最终文件
        if (File.Exists(finalFilePath))
        {
            File.Delete(finalFilePath);
        }
        File.Move(tempFilePath, finalFilePath);
        Debug.Log($"文件已保存至: {finalFilePath}");
    }

    private void CloseFileStream()
    {
        if (fileStream != null)
        {
            fileStream.Flush();
            fileStream.Close();
            fileStream.Dispose();
            fileStream = null;
        }
    }

    // 清理资源
    public new void Dispose()
    {
        CloseFileStream();
        base.Dispose();
    }
}