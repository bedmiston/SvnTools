using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using SvnTools;
using SvnTools.CommandLine;

// $Id$

namespace SvnBackup
{
   class Program
   {
      static int Main(string[] args)
      {
         log4net.Config.XmlConfigurator.Configure();

         if (Parser.ParseHelp(args))
         {
            OutputHeader();
            OutputUsageHelp();
            return 0;
         }

         StringBuilder errorBuffer = new StringBuilder();
         BackupArguments arguments = new BackupArguments();
         if (!Parser.ParseArguments(args, arguments, s => errorBuffer.AppendLine(s)))
         {
            OutputHeader();
            Console.Error.WriteLine(errorBuffer.ToString());
            OutputUsageHelp();
            return 1;
         }

         try
         {
            Backup.Run(arguments);
         }
         catch(Exception ex)
         {
            SendErrorEmail(ex);
            return 1;
         }
         

         return 0;
      }

      private static void SendErrorEmail(Exception ex)
      {
         var emailMessage = new MimeMessage();

         emailMessage.From.Add(new MailboxAddress(ConfigurationManager.AppSettings["EmailFrom"]));
         string[] toAddresses = ConfigurationManager.AppSettings["EmailTo"].Split(new char[] { ',', '|', ';' });
         foreach (string address in toAddresses)
         {
            emailMessage.To.Add(new MailboxAddress(address));
         }
         emailMessage.Subject = "Exception Backing Up SVN Repositories";

         var builder = new BodyBuilder();
         StringBuilder htmlBody = new StringBuilder();
         htmlBody.Append(string.Format("<H1>SVNBackup Error - ERROR</H1><H2>{0}</H2>", ex.Message));
         if (ex != null)
         {
            htmlBody.Append(string.Format("<pre>{0}</pre>", ex));
         }
         StringBuilder textBody = new StringBuilder();
         textBody.Append(string.Format(">SVNBackup Error - ERROR\r\n\r\n{0}", ex.Message));
         if (ex != null)
         {
            textBody.Append(string.Format("\r\n{0}", ex));
         }


         if (textBody.Length > 0) { builder.TextBody = textBody.ToString(); }
         if (htmlBody.Length > 0) { builder.HtmlBody = htmlBody.ToString(); }

   
         emailMessage.Body = builder.ToMessageBody();
         using (var client = new SmtpClient())
         {
            client.Connect(ConfigurationManager.AppSettings["SMTPServer"], 25, SecureSocketOptions.None);
            client.Send(emailMessage);
            client.Disconnect(true);
         }

      }

      private static void OutputUsageHelp()
      {
         Console.WriteLine();
         Console.WriteLine("SvnBackup.exe /r:<directory> /b:<directory> /c");
         Console.WriteLine();
         Console.WriteLine("     - BACKUP OPTIONS -");
         Console.WriteLine();
         Console.WriteLine(Parser.ArgumentsUsage(typeof(BackupArguments)));
      }

      private static void OutputHeader()
      {
         Console.WriteLine("SvnBackup v{0}", ThisAssembly.AssemblyInformationalVersion);
         Console.WriteLine();
      }
   }
}
