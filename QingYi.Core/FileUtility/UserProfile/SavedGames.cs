using System;
using System.IO;

namespace QingYi.Core.FileUtility.UserProfile
{
    /// <summary>
    /// <b>Windows Only!</b><br />
    /// Provides functionality for managing saved games, including creating files and folders.<br/>
    /// 提供管理已保存游戏的功能，包括创建文件和文件夹。
    /// </summary>
    public partial class SavedGames
    {
        /// <summary>
        /// Gets the path to the saved games directory.<br/>
        /// 获取已保存游戏目录的路径。
        /// </summary>
        public static string path => Get();

        /// <summary>
        /// Gets the path to the saved games directory.<br/>
        /// 获取已保存游戏目录的路径。
        /// </summary>
        /// <returns>The full path to the saved games directory.<br/>已保存游戏目录的完整路径。</returns>
        public static string Get() => Path.Combine(Profile.UserProfilePath, "Saved Games");

        /// <summary>
        /// Creates a new empty file with the specified name and returns the full path to the created file.<br/>
        /// 创建指定名称的新空文件，并返回创建的文件的完整路径。
        /// </summary>
        /// <param name="fileName">The name of the new file to create.<br/>要创建的新文件的名称。</param>
        /// <returns>The full path to the newly created file.<br/>新创建的文件的完整路径。</returns>
        /// <exception cref="Exception">If an error occurs during file creation.<br/>如果在文件创建过程中发生错误。</exception>
        public static string CreateFile(string fileName)
        {
            string newFilePath = Path.Combine(Get(), fileName);

            try
            {
                // 创建空文件
                using (File.Create(newFilePath)) { };
                return newFilePath;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Creates a new file with the specified name and content, and returns the full path to the created file.<br/>
        /// 创建指定名称和内容的新文件，并返回创建的文件的完整路径。
        /// </summary>
        /// <param name="fileName">The name of the new file to create.<br/>要创建的新文件的名称。</param>
        /// <param name="content">The content to write to the new file.<br/>要写入新文件的内容。</param>
        /// <returns>The full path to the newly created file.<br/>新创建的文件的完整路径。</returns>
        /// <exception cref="Exception">If an error occurs during file creation.<br/>如果在文件创建过程中发生错误。</exception>
        public static string CreateFile(string fileName, string content)
        {
            string newFilePath = Path.Combine(Get(), fileName);

            try
            {
                // 创建文件并写入内容
                using (FileStream fs = File.Create(newFilePath))
                {
                    // 使用 StreamWriter 写入内容
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.Write(content);
                    }
                }
                return newFilePath;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Creates a new folder with the specified name and returns the full path to the created folder.<br/>
        /// 创建指定名称的新文件夹，并返回创建的文件夹的完整路径。
        /// </summary>
        /// <param name="folderName">The name of the new folder to create.<br/>要创建的新文件夹的名称。</param>
        /// <returns>The full path to the newly created folder.<br/>新创建的文件夹的完整路径。</returns>
        /// <exception cref="Exception">If an error occurs during folder creation.<br/>如果在文件夹创建过程中发生错误。</exception>
        public static string CreateFolder(string folderName)
        {
            string newFolderPath = Path.Combine(Get(), folderName);

            try
            {
                Directory.CreateDirectory(newFolderPath);

                return newFolderPath;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
