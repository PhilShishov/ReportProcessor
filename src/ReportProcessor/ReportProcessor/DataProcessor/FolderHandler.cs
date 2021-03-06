﻿namespace ReportProcessor.DataProcessor
{
    using System;
    using System.Data;
    using System.IO;
    using System.Linq;

    using NLog;
    using ReportProcessor.Common;
    using ReportProcessor.Data.Models;
    using ReportProcessor.Data.Services;
    using ToDataTable;

    public class FolderHandler
    {
        public static void ProcessFolder(string reportDir, string fullFolderPath, Logger logger, bool isLinux)
        {
            var fileArray = Directory.EnumerateFiles(fullFolderPath, "*.*")
                .Where(fn => fn.ToLower().EndsWith(GlobalConstants.ExcelFileExtension) ||
                             fn.ToLower().EndsWith(GlobalConstants.CSVFileExtension))
                .OrderBy(f => f)
                .ToArray();

            logger.Info(string.Format(InfoMessages.CheckingFolder, reportDir));
            logger.Info(string.Format(InfoMessages.FileCountInFolder, fileArray.Length));

            for (int i = 0; i < fileArray.Length; i++)
            {
                var projectDir = GetProjectDirectory();
                var provider = RetrieveHeadersFromProvider(reportDir, projectDir + GlobalConstants.FolderHeaders);
                var fullFilePath = fileArray[i];
                var currentFileName = Path.GetFileName(fileArray[i]);

                try
                {
                    logger.Info(string.Format(InfoMessages.ProcessingFile, currentFileName));
                    var csvList = DataHandler.ProcessData(provider, fullFilePath, logger);

                    string folderOnError = isLinux ?
                        GlobalConstants.LinuxFolderOnError :
                        GlobalConstants.WindowsFolderOnError;

                    string folderOnSuccess = isLinux ?
                       GlobalConstants.LinuxFolderOnSuccess :
                       GlobalConstants.WindowsFolderOnSuccess;

                    if (csvList == null || csvList.Count == 0)
                    {
                        logger.Info(string.Format(InfoMessages.FileMoveError, currentFileName));
                        File.Move(fullFilePath, string.Format(folderOnError + currentFileName, reportDir));
                        logger.Info(string.Format(InfoMessages.LineHeader, reportDir));
                        continue;
                    }

                    DataTable csvData = csvList.ToDataTable();
                    logger.Info(InfoMessages.RowsCountFile + csvData.Rows.Count);

                    bool isInserted = SqlService.UploadData(csvData, logger);

                    if (!isInserted)
                    {
                        logger.Info(string.Format(InfoMessages.FileMoveError, currentFileName));
                        File.Move(fullFilePath, string.Format(folderOnError + currentFileName, reportDir));
                        continue;
                    }

                    if (!File.Exists(folderOnSuccess + currentFileName))
                    {
                        logger.Info(string.Format(InfoMessages.FileMoveSuccess, currentFileName));
                        File.Move(fullFilePath, string.Format(folderOnSuccess + currentFileName, reportDir));
                    }
                }
                catch (IOException ex)
                {
                    logger.Error(ex.Message);
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                }
            }
        }

        private static Provider RetrieveHeadersFromProvider(string reportDir, string baseDir)
            => Deserializer.ImportHeaders(File.ReadAllText(baseDir + string.Format(GlobalConstants.HeadersFileName, reportDir)));

        private static string GetProjectDirectory()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var directoryName = Path.GetFileName(currentDirectory);
            var relativePath = directoryName.StartsWith(GlobalConstants.NetCoreApp) ? @"../../../" : string.Empty;

            return relativePath;
        }
    }
}
