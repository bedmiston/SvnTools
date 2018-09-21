﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zip;
using SvnTools.Services;
using SvnTools.Utility;

// $Id$

namespace SvnTools
{
   /// <summary>
   /// A class to backup subversion repositories.
   /// </summary>
   public static class Backup
   {
      #region Logging Definition
      private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(typeof(Backup));
      #endregion

      /// <summary>
      /// Runs a backup with the specified <see cref="BackupArguments"/>.
      /// </summary>
      /// <param name="args">The arguments used in the backup.</param>
      public static void Run(BackupArguments args)
      {
         var repoRoot = new DirectoryInfo(args.RepositoryRoot);
         if (!repoRoot.Exists)
            throw new InvalidOperationException(string.Format(
                "The repository root directory '{0}' does not exist.",
                args.RepositoryRoot));

         var backupRoot = new DirectoryInfo(args.BackupRoot);
         if (!backupRoot.Exists)
            backupRoot.Create();

         // first try repoRoot as a repository
         if (PathHelper.IsRepository(repoRoot.FullName))
            BackupRepository(args, repoRoot, backupRoot);
         // next try as parent folder for repositories
         else
         {
            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = args.Threads;

            Parallel.ForEach(repoRoot.GetDirectories(), po, (repo) =>
            {
               try
               {
                  BackupRepository(args, repo, backupRoot);
               }
               catch (Exception ex)
               {
                  _log.Error("An exception occurred while backing up repos", ex);
                  throw;
               }
            });
         }
      }

      
      private static void BackupRepository(BackupArguments args, DirectoryInfo repository, DirectoryInfo backupRoot)
      {
         try
         {
            string revString = GetRevision(args, repository);

            if (string.IsNullOrEmpty(revString))
               return; // couldn't find repo

            var skipRepositories = args.SkipRepositories.Split(',');
            foreach (var skipRepository in skipRepositories)
            {
               if (string.Compare(skipRepository, repository.Name, true) == 0)
               {
                  _log.InfoFormat("Skipping '{0}' because it is in the list of repositories to skip.", repository.Name);
                  return;
               }
            }

            string backupRepoPath = Path.Combine(backupRoot.FullName, repository.Name);
            string backupRevPath = Path.Combine(backupRepoPath, revString);
            string backupZipPath = backupRevPath + ".zip";

            if (!Directory.Exists(backupRepoPath))
               Directory.CreateDirectory(backupRepoPath);

            if (Directory.Exists(backupRevPath) || File.Exists(backupZipPath))
            {
               _log.InfoFormat("Skipping '{0}' from '{1}' because it already exists.", revString, repository.Name);
               return; // this rev is already backed up
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            // hotcopy
            _log.InfoFormat("Backing up '{0}' from '{1}'.", revString, repository.Name);
            RunHotCopy(args, repository, backupRevPath);

            // compress
            if (args.Compress && !File.Exists(backupZipPath))
               CompressBackup(backupRevPath, backupZipPath);

            // purge old
            PruneBackups(backupRepoPath, args.History);
            _log.InfoFormat("BackupRepository() complete for '{0}'. Duration: {1}.", repository.Name, stopwatch.Elapsed);
         }
         catch (Exception ex)
         {
            _log.Error(ex.Message, ex);
         }
      }

      private static void RunHotCopy(BackupArguments args, DirectoryInfo repo, string backupRevPath)
      {
         using (var hotCopy = new HotCopy())
         {
            if (!string.IsNullOrEmpty(args.SubverisonPath))
               hotCopy.ToolPath = args.SubverisonPath;

            hotCopy.BackupPath = backupRevPath;
            hotCopy.RepositoryPath = repo.FullName;

            hotCopy.Execute();

            if (!string.IsNullOrEmpty(hotCopy.StandardError))
               _log.Info(hotCopy.StandardError);
         }

         _log.InfoFormat("Backup of {0} complete.", backupRevPath);
      }

      private static string GetRevision(BackupArguments args, DirectoryInfo repo)
      {
         int rev;

         // version
         using (var look = new SvnLook(SvnLook.Commands.Youngest))
         {
            look.RepositoryPath = repo.FullName;
            if (!string.IsNullOrEmpty(args.SubverisonPath))
               look.ToolPath = args.SubverisonPath;

            look.Execute();
            if (!string.IsNullOrEmpty(look.StandardError))
               _log.Info(look.StandardError);

            if (!look.TryGetRevision(out rev))
            {
               _log.WarnFormat("'{0}' is not a repository.", repo.Name);

               if (!string.IsNullOrEmpty(look.StandardOutput))
                  _log.Info(look.StandardOutput);

               return null;
            }
         }

         return "v" + rev.ToString().PadLeft(7, '0');
      }

      private static void CompressBackup(string backupRevPath, string backupZipPath)
      {
         using (var zipFile = new ZipFile())
         {
            // for large zip files
            zipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;
            zipFile.UseUnicodeAsNecessary = true;

            zipFile.AddDirectory(backupRevPath);
            zipFile.Save(backupZipPath);
         }

         PathHelper.DeleteDirectory(backupRevPath);

         _log.InfoFormat("Zip {0} complete.", backupZipPath);
      }

      private static void PruneBackups(string backupRepoPath, int historyCount)
      {
         if (historyCount < 1)
            return;

         var dirs = Directory.GetDirectories(backupRepoPath);
         if (dirs.Length > historyCount)
         {
            for (int i = 0; i < dirs.Length - historyCount; i++)
            {
               string dir = dirs[i];

               PathHelper.DeleteDirectory(dir);
               _log.InfoFormat("Removed backup '{0}'.", dir);
            }
         }

         var files = Directory.GetFiles(backupRepoPath, "*.zip");
         if (files.Length > historyCount)
         {
            for (int i = 0; i < files.Length - historyCount; i++)
            {
               string file = files[i];

               File.Delete(file);
               _log.InfoFormat("Removed backup '{0}'.", file);
            }
         }
      }

   }
}
