using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace ForkStalker
{
	class MainClass
	{

		public static void Main (string[] args)
		{
			var regressionCheck = false;
			const String repoPath = "/Users/administrator/code/playscript/playscriptredux/playscript";
			const String sourcesFileName = "CI/" + "playscript.sources";
			const String ignoreListFileName = "CI/" + "playscript.sources.ignore.lst";

			var stalkBranch = "master";
			var stalkNextTag = "mono-4.0.0.143";

			var repo = new Stalker (repoPath, sourcesFileName, ignoreListFileName, "merges-to-mono-4.0.0.143", stalkNextTag, stalkBranch);

			var play = new Regression (RegressType.play, repoPath);
			var mono = new Regression (RegressType.mono, repoPath);

			// git log --reverse --name-only playscript..master
			var commitList = repo.StalkerList;
			Console.WriteLine (commitList.Count ());

			var cc = new ConsoleCopy(Path.Combine(repoPath, "ForkStalker.log"));

			var localCommitList = new List<Commit> ();
			var okToContinue = true;
			while (okToContinue) {
				localCommitList.Clear ();
				commitList = repo.StalkerList;
				foreach (var commit in commitList.Take (2000)) {
					regressionCheck = false;
					Array.ForEach (repo.CommitLog (commit, false).ToArray (), Console.WriteLine);

					var localList = repo.StalkerFiles (commit);
					if (localList.Any ()) {
//						Console.WriteLine ("!!! {0}", localList.Count ());
//						Array.ForEach (repo.StalkerFileListLog (commit).ToArray (), Console.WriteLine);
//					Console.ReadKey ();
						// Merge the last fastforward commit
						MergeResult mergeResult;
						if (localCommitList.Any ()) {
							// Merge up to, but before the commit that contains stalker files 
							mergeResult = repo.StalkUpTo (localCommitList);
							// Now merge the commit that includes stalker files
							if (mergeResult.Commit != null) {
								regressionCheck = true;
//								Console.WriteLine ("*** Stalker alert, files need reviewed ***");
//								Array.ForEach (repo.StalkerFileListLog (commit).ToArray (), Console.WriteLine);

								// Save log for .jay changes so we can bring parity to mcs/mcs/ps-parser.jay later
								if (repo.FilesToMerge (commit).Contains ("mcs/mcs/cs-parser.jay")) {
									Console.WriteLine ("\n");
									Console.WriteLine ("Changed: mcs/mcs/cs-parser.jay, appending log");
									Console.WriteLine ("Commit : {0}", commit.Sha);
									Console.WriteLine ("Msg : {0}", commit.Message);
									Array.ForEach (repo.CommitLog (commit, true).ToArray (), Console.WriteLine);
									Console.WriteLine ("\n");
								}
								mergeResult = repo.StalkMerge (commit);
							} 
							if (mergeResult.Status == MergeStatus.Conflicts) {
								// List file Conflicts
								var status = repo.Status;
								var fgColor = Console.ForegroundColor;
								var bgColor = Console.BackgroundColor;

								Console.ForegroundColor = ConsoleColor.DarkYellow;
								Console.WriteLine ("Commit : {0}", commit.Sha);
								Console.WriteLine ("Info : {0} / {1} / {2}", commit.Author.When, commit.Author.Name, commit.Author.Email);
								Console.WriteLine ("Msg : {0}", commit.Message);

								Console.ForegroundColor = ConsoleColor.DarkGreen;
								foreach (var file in status.Staged) {
									Console.WriteLine ("{0} : {1}", file.State, file.FilePath);
								}
								Console.ForegroundColor = ConsoleColor.Red;
								foreach (var file in status.Modified) {
									Console.WriteLine ("{0} : {1}", file.State, file.FilePath);
								}
								foreach (var conflict in repo.Index.Conflicts) {
									Console.ForegroundColor = ConsoleColor.Red;
									Console.WriteLine ("Conflict : {0}", conflict.Ours.Path);
									if (conflict.Ours.Path == "README.md") {
										Console.ForegroundColor = ConsoleColor.DarkGreen;
										Console.WriteLine (" ~ Checkout --ours {0}", conflict.Ours.Path);
										repo.CheckoutOurs (conflict);
										continue;
									}
									if (conflict.Ours.Path == "CONTRIBUTING.md") {
										Console.ForegroundColor = ConsoleColor.DarkGreen;
										Console.WriteLine (" ~ Checkout --ours {0}", conflict.Ours.Path);
										repo.CheckoutOurs (conflict);
										continue;
									}
									if (repo.sources.Contains(conflict.Ours.Path)) {
										Console.ForegroundColor = ConsoleColor.DarkYellow;
										Console.WriteLine (" ~ Manual intervention needed : {0}", conflict.Ours.Path);
									} else {
										Console.ForegroundColor = ConsoleColor.DarkGreen;
										Console.WriteLine (" ~ Checkout --theirs {0}", conflict.Ours.Path);
										try {
											repo.CheckoutTheirs (conflict);
										} catch {
											Console.ForegroundColor = ConsoleColor.DarkRed;
											Console.WriteLine (" ~ !! FAILED: Checkout --theirs {0}", conflict.Ours.Path);
										}
										continue;
									}
								}
								Console.ForegroundColor = fgColor;
								Console.BackgroundColor = bgColor;
								okToContinue = false;
								break;
							}
							if (mergeResult.Status == MergeStatus.NonFastForward) {
								if (regressionCheck) {
									Console.Write ("Running Play Regression...");
									var playResult = play.RunRegression (true);
									Console.WriteLine ("Status : {0}", playResult);
									if (playResult != 0) {
										var tmpColor = Console.ForegroundColor;
//										if (play.outputStdErr.Count > 0) {
//											Console.ForegroundColor = ConsoleColor.Red;
//											Array.ForEach (play.outputStdErr.ToArray (), Console.WriteLine);
//										}
										if (play.outputStdOut.Count > 0) {
											Array.ForEach (play.outputStdOut.ToArray (), Console.WriteLine);
										}
										Console.ForegroundColor = tmpColor;
										okToContinue = false;
										break;
									} else {
										// Regression passsed, commit the merge
										var sCommit = repo.StalkerCommit ();
										if (sCommit == null)
											okToContinue = false;
											break;
									}
								}
								okToContinue = true;
								break;
							}
						}
					} else {
						localCommitList.Add (commit);
					}
				}
			}
			
			cc.Dispose ();
		}
	}
}
