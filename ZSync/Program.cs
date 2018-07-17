using Fclp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ZSync.Helpers;
using Microsoft.Synchronization.Files;
using Microsoft.Synchronization;
using System.Threading;

namespace ZSync
{
    class Program
    {

        static ManualResetEvent _quitEvent = new ManualResetEvent(false);
        static bool helpDisplayed = false;

        static void Main(string[] args)
        {

            try
            {
                ApplicationArguments myArgs = ParseArgs(args);

                if (!helpDisplayed)
                {
                    Console.WriteLine("** Start Sync **"); Console.WriteLine("");


                    if (Validation(myArgs))
                    {
                        Console.WriteLine(""); Console.WriteLine("-- Change Log --");
                        DoSync(myArgs);
                    }

                    Console.WriteLine(""); Console.WriteLine("** Sync Completed **");

                    if (myArgs.Pause)
                    {
                        Console.CancelKeyPress += (sender, eArgs) =>
                        {
                            _quitEvent.Set();
                            eArgs.Cancel = true;
                        };
                        Console.WriteLine("Press Ctrl+C to exit.");
                        _quitEvent.WaitOne();

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Error: {0}", ex.Message));
                Console.CancelKeyPress += (sender, eArgs) => {
                    _quitEvent.Set();
                    eArgs.Cancel = true;
                };
                Console.WriteLine(""); Console.WriteLine("** Sync Completed **");
                Console.WriteLine("Press Ctrl+C to exit.");
                _quitEvent.WaitOne();
            }



        }

        /// <summary>
        /// Parse the command line args
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Class with populated arguments</returns>
        static ApplicationArguments ParseArgs(string[] args)
        {

            ApplicationArguments myArgs = new ApplicationArguments();
            myArgs.Help = false;

            // create a builder for the ApplicationArguments type
            var b = new FluentCommandLineParser<ApplicationArguments>();

            // specify which property the value will be assigned too.
            b.Setup<String>(arg => arg.From)
             .As('f', "from") // define the short and long option name
             .Required() // using the standard fluent Api to declare this Option as required.
             .WithDescription("Folder files are copied from");

            b.Setup<String>(arg => arg.To)
             .As('t', "to")
             .Required()
             .WithDescription("Folder files are copied to");

            b.Setup<bool>(arg => arg.BothWays)
             .As('b', "bothways")
             .SetDefault(false)
             .WithDescription("Full Synchrionisation");

            b.Setup<String>(arg => arg.Include)
             .As('i', "include")
             .WithDescription("Include filter");

            b.Setup<String>(arg => arg.Exclude)
             .As('x', "exclude")
             .WithDescription("Exclude filter");


            b.Setup<bool>(arg => arg.Pause)
             .As('p', "pause")
             .SetDefault(false)
             .WithDescription("Pause on completion");

            b.SetupHelp("?", "help")
                .Callback(text =>
                {
                    Console.WriteLine(@"ZSync is built on 'Microsoft Sync Framework'. This will synchronise files between two folders, keeping track of changes between sync's.");
                    Console.WriteLine("");
                    Console.Write(text);
                    helpDisplayed = true;
                });

            var result = b.Parse(args);
            
            myArgs = (ApplicationArguments)b.Object;

            //if (args[0] == "-?" || args[0] == "--?" || args[0].ToUpper() == "-HELP" || args[0].ToUpper() == "--HELP")
            //{
            //    myArgs.Help = true;
            //}
            //else

            if (!helpDisplayed)
            {
                myArgs.Help = false;
                Console.WriteLine(String.Format("From Folder: {0}", myArgs.From));
                Console.WriteLine(String.Format("To Folder: {0}", myArgs.To));
                Console.WriteLine("Direction: {0}", myArgs.BothWays ? "Two Way" : "One Way");
                if (!String.IsNullOrEmpty(myArgs.Include)) Console.WriteLine(String.Format("Include: {0}", myArgs.Include));
                if (!String.IsNullOrEmpty(myArgs.Exclude)) Console.WriteLine(String.Format("Exclude: {0}", myArgs.Exclude));
                if (myArgs.Pause) Console.WriteLine("Pause: Pause on complete");
                Console.WriteLine("");
            }

            return myArgs;
        }

        /// <summary>
        /// Validate arguments
        /// </summary>
        /// <param name="myArgs"></param>
        /// <returns></returns>
        static bool Validation(ApplicationArguments myArgs)
        {

            bool ok = true;

            if (string.IsNullOrEmpty(myArgs.From) || !Directory.Exists(myArgs.From))
            {
                Console.WriteLine(String.Format("From path does not exist: {0}", myArgs.From));
                ok = false;
            }

            if (string.IsNullOrEmpty(myArgs.To) || !Directory.Exists(myArgs.To))
            {
                Console.WriteLine(String.Format("To path does not exist: {0}", myArgs.To));
                ok = false;
            }

            return ok;
        }


        private static void DoSync(ApplicationArguments myArgs)
        {
            try
            {

                Mediator.Register("update", AddUpdate); // Listener for change events


                FileSyncOptions options = FileSyncOptions.ExplicitDetectChanges
                    | FileSyncOptions.RecycleConflictLoserFiles
                    | FileSyncOptions.RecycleDeletedFiles
                    | FileSyncOptions.RecyclePreviousFileOnUpdates;

                FileSyncScopeFilter filter = new FileSyncScopeFilter();

                if (!String.IsNullOrEmpty(myArgs.Include))
                    filter.FileNameIncludes.Add(myArgs.Include);

                if (!String.IsNullOrEmpty(myArgs.Exclude))
                    filter.FileNameExcludes.Add(myArgs.Exclude);

                // Avoid two change detection passes for the two-way sync
                FindFileSystemReplicaChanges(myArgs.From, filter, options);
                FindFileSystemReplicaChanges(myArgs.To, filter, options);

                // Sync both ways
                OneWaySyncFileSystemReplicas(myArgs.From, myArgs.To, null, options);
                if (myArgs.BothWays)
                {
                    OneWaySyncFileSystemReplicas(myArgs.To, myArgs.From, null, options);
                }

            }
            catch (Exception exc)
            {
                Console.WriteLine(String.Format("Exception thrown while syncing: {0}", exc.Message));
            }
            finally
            {
                Mediator.Unregister("update", AddUpdate);
            }
        }


        public static void FindFileSystemReplicaChanges(string replicaRootPath, FileSyncScopeFilter filter, FileSyncOptions options)
        {
            FileSyncProvider provider = null;

            try
            {
                provider = new FileSyncProvider(replicaRootPath, filter, options);
                provider.DetectChanges();
            }
            finally
            {
                if (provider != null)
                    provider.Dispose();
            }
        }

        public static void OneWaySyncFileSystemReplicas(string sourceReplicaRootPath, string destinationReplicaRootPath, FileSyncScopeFilter filter, FileSyncOptions options)
        {
            FileSyncProvider path1Provider = null;
            FileSyncProvider path2Provider = null;

            try
            {
                path1Provider = new FileSyncProvider(sourceReplicaRootPath, filter, options);
                path2Provider = new FileSyncProvider(destinationReplicaRootPath, filter, options);

                path2Provider.SkippedChange += OnSkippedChange;
                path2Provider.AppliedChange += OnAppliedChange;

                SyncOrchestrator manager = new SyncOrchestrator();
                manager.LocalProvider = path1Provider;
                manager.RemoteProvider = path2Provider;
                manager.Direction = SyncDirectionOrder.Upload;

                manager.Synchronize();
            }
            finally
            {
                if (path1Provider != null)
                    path1Provider.Dispose();
                if (path2Provider != null)
                    path2Provider.Dispose();
            }
        }



        public static void OnAppliedChange(object sender, AppliedChangeEventArgs args)
        {
            switch (args.ChangeType)
            {
                case ChangeType.Create:
                    Mediator.NotifyColleagues("update", "File created: " + args.NewFilePath);
                    break;
                case ChangeType.Delete:
                    Mediator.NotifyColleagues("update", "Deleted File: " + args.OldFilePath);
                    break;
                case ChangeType.Update:
                    Mediator.NotifyColleagues("update", "Overwrote file: " + args.OldFilePath);
                    break;
                case ChangeType.Rename:
                    Mediator.NotifyColleagues("update", "Renamed file: " + args.OldFilePath + " to " + args.NewFilePath);
                    break;
            }
        }

        public static void OnSkippedChange(object sender, SkippedChangeEventArgs args)
        {
            Mediator.NotifyColleagues("update", "Error! Skipped file: " + args.ChangeType.ToString().ToUpper() + " for "
                + (!string.IsNullOrEmpty(args.CurrentFilePath) ? args.CurrentFilePath : args.NewFilePath));

            if (args.Exception != null)
                Mediator.NotifyColleagues("update", "Error: " + args.Exception.Message);
        }

        private static void AddUpdate(object param)
        {
            Console.WriteLine(String.Format("{0}", param));
        }

    }

    public class ApplicationArguments
    {
        public String From { get; set; }
        public String To { get; set; }
        public Boolean BothWays { get; set; }
        public String Include { get; set; }
        public String Exclude { get; set; }
        public Boolean Pause { get; set; }
        public Boolean Help { get; set; }
    }
}
