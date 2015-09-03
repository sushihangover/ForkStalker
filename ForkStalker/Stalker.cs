using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoreLinq;
using LibGit2Sharp;
using System.Globalization;

namespace ForkStalker
{
	public class Stalker
	{
		[DllImport ("libc")]
		private static extern int system (string exec);

		readonly Repository repo;
		readonly Tag tag;
		readonly Branch master;
		readonly Branch head;
		//		readonly Branch headTag;
		readonly String[] ignore;
		readonly public String[] sources;
		readonly String userName = "SushiHangover";
		readonly String userEmail = "sushihangover@outlook.com";
		String commitMsg;

		//readonly Tree masterTree;
		//readonly Tree currentTree;

		public Stalker (String localRepo, String sourceFileName, String ignoreFileName, String localStalkBranch = null, String targetTag = null, String localMaster = "master")
		{
			repo = new Repository (localRepo);

			master = repo.Branches.Single (branch => branch.FriendlyName == localMaster);

			if (targetTag != null) {
				tag = repo.Tags.Single (t => t.FriendlyName == targetTag);
			}

//			if (localStalkBranch != null)
				head = repo.Branches.Single (branch => branch.FriendlyName == localStalkBranch);
//			else 
//				head = repo.Head;

//			if (head != repo.Branches.Single (b => b.IsCurrentRepositoryHead)) {
//                repo.Checkout(head);
//			}
//            head = repo.Branches.Single(b => b.IsCurrentRepositoryHead);
			//var filter = new CommitFilter { Since = repo.Branches["master"], Until = repo.Branches["development"] };
			//masterTree = repo.Lookup<Tree>(master.Tip.Sha);
			//currentTree = repo.Lookup<Tree>(head.Tip.Sha);
			try {
				sources = File.ReadAllLines (Path.Combine (localRepo, sourceFileName));
			} catch {
			} finally {
			}
			try {
				ignore = File.ReadAllLines (Path.Combine (localRepo, ignoreFileName));
			} catch {
			} finally {
			}
		}

		public RepositoryStatus Status {
			get {
				return repo.RetrieveStatus ();
			}
		}

		public Index Index {
			get {
				return repo.Index;
			}
		}

		//git log ^playscript master --merges --pretty=oneline| tail -1
		//	b07a494bb6b734a58268066533b1db2f884d6eba Merge remote-tracking branch 'upstream/master'
		// get the last merge commit which is the merge commit of master and topic.
		public Commit LastMergeCommit {
			get {
				var filter = new CommitFilter { 
					SortBy = CommitSortStrategies.Time,
					Since = master,
					Until = head,
				};
				var completeList = repo.Commits.QueryBy (filter);
				var mergeList = completeList.Where (p => p.Parents.Count () >= 2);
				return mergeList.Last ();
			}
		}

		// git log playscript --not $(git rev-list master ^playscript --merges | tail -1)^
		public ICommitLog NotMergedList {
			get {
				//var commit = LastMergeCommit;
				var filter = new CommitFilter { 
					SortBy = CommitSortStrategies.Time | CommitSortStrategies.Reverse,
					Since = master.Tip,
					Until = LastMergeCommit,
				};
				return repo.Commits.QueryBy (filter);
			}
		}
        
		// We are how many steps away from our goal
		public int StepsToGo {
			get {
				return StalkerList.Count ();
			}
		}

		// We are how many steps away from our tag goal
		public int StepsToGoToNextTag {
			get {
				return StalkerList.Count ();
			}
		}

		// git log HEAD..master --reverse
		public ICommitLog StalkerList {
			get {
				var filter = new CommitFilter { 
					SortBy = CommitSortStrategies.Reverse | CommitSortStrategies.Time,
					Since = master,
					Until = head.Tip,             
				};
				return repo.Commits.QueryBy (filter);
			}
		}

		// git log HEAD..master --reverse|head -1
		public Commit StalkerNextStep {
			get {
				var filter = new CommitFilter { 
					SortBy = CommitSortStrategies.Reverse | CommitSortStrategies.Time,
					Since = master.Tip,
					Until = LastMergeCommit, //head.Tip,                    
				};
				return repo.Commits.QueryBy (filter).First ();
			}
		}

		public bool IsStalkerHere {
			get {
				var commitFiles = FilesToMerge (StalkerNextStep);
				return (sources.Intersect (commitFiles).Any () ? true : false);
			}
		}

		public bool IsStalkingRequiredFor (Commit commit)
		{
			var commitFiles = FilesToMerge (commit);
			return (sources.Intersect (commitFiles).Any () ? true : false);               
		}
        
		// git log --name-status --pretty=oneline --reverse HEAD..master
		public String[] FilesToMerge (Commit commit)
		{
			var fileList = new List<String> ();
			foreach (var parent in commit.Parents) {
				foreach (TreeEntryChanges change in repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree)) {
					fileList.Add (change.Path);
				}
			}
			return fileList.ToArray ();
		}

		public String[] StalkerFiles (Commit commit)
		{
			var commitFiles = FilesToMerge (commit);
			return sources.Intersect (commitFiles).ToArray ();
		}

		// Include all the commits
		public IReadOnlyList<Commit> StalkerSteps {
			get {
				var shas = new List<Commit> ();
				foreach (var commit in StalkerList) {
					shas.Add (commit);                
					if (IsStalkingRequiredFor (commit)) {
						break;
					}
				}
				return shas.AsReadOnly ();
			}
		}

		// Just the next commit that needs merged to our source file list
		public Commit MeetMyStalker {
			get {
				foreach (var commit in StalkerList) {
					if (IsStalkingRequiredFor (commit)) {
						return commit;
					}
				}
				return null;
			}
		}
        
		// Up to, but do not include the commit that includes files we are stalking
		public IReadOnlyList<Commit> StalkAndHideList {
			get {
				var shas = new List<Commit> ();
				foreach (var commit in StalkerList) {
					if (IsStalkingRequiredFor (commit)) {
						break;
					}
					shas.Add (commit);                
				}
				return shas.AsReadOnly ();
			}
		}

		public List<String> StalkerFileListLog (Commit commit, List<String> log = null)
		{
			if (log == null)
				log = new List<String> ();

			var mergelist = FilesToMerge (commit);
			var localList = StalkerFiles (commit);
//			foreach (var filename in mergelist) {
//				if (localList.Contains (filename))
//					log.Add (String.Format ("{0}\t\t{1}", localList.Contains (filename) ? "*" : "", filename));
//			}
			return log;
		}


		private List<String> CommitFileListLog (Commit commit, List<String> log = null)
		{
			if (log == null)
				log = new List<String> ();

			var mergelist = FilesToMerge (commit);
			var localList = StalkerFiles (commit);
			foreach (var filename in mergelist) {
				log.Add (String.Format ("{0}\t\t{1}", localList.Contains (filename) ? "*" : "", filename));
			}
			return log;
		}

		public List<String> CommitLog (Commit commit, Boolean includeFileList = false, List<String> log = null)
		{
			if (log == null)
				log = new List<String> ();

			if (commit != null) {
				log.Add (String.Format ("{0}\t{1} {2} {3}", "", commit.Committer.When.ToString ("s", CultureInfo.InvariantCulture), commit.Sha.Substring (0, 6), commit.MessageShort.PadRight (60, ' ').Substring (0, 40)));
				if (includeFileList)
					log = CommitFileListLog (commit, log);
			}
			return log;
		}


		// StalkerLog().ToList().ForEach(line => Console.WriteLine("{0}", line));
		// Array.ForEach(StalkerLog(), Console.WriteLine);
		public List<String> MeetMyStalkerLog (Boolean includeFileList = false, List<String> log = null)
		{
			if (log == null)
				log = new List<String> ();

			var lastCommit = MeetMyStalker;
			if (lastCommit != null) {
				log.Add (String.Format ("{0}\t{1} {2} {3}", "*", lastCommit.Committer.When.ToString ("s", CultureInfo.InvariantCulture), lastCommit.Sha.Substring (0, 6), lastCommit.MessageShort.PadRight (60, ' ').Substring (0, 40)));
				if (includeFileList)
					log = CommitFileListLog (lastCommit, log);
			}
			return log;
		}

		public List<String> StalkAndHideLog (Boolean includeFileList = false, List<String> log = null)
		{
			if (log == null)
				log = new List<String> ();
			
			foreach (var commit in StalkAndHideList) {
				log.Add (String.Format ("{0}\t{1} {2} {3}", "", commit.Committer.When.ToString ("s", CultureInfo.InvariantCulture), commit.Sha.Substring (0, 6), commit.MessageShort.PadRight (60, ' ').Substring (0, 40)));                                    
				if (includeFileList)
					log = CommitFileListLog (commit, log);
			}
			return log;
		}



		public List<String> MergeResultLog (MergeResult result, List<String> log = null)
		{
			if (log == null)
				log = new List<String> ();

			log.Add (String.Format ("!! {0} : {1}", result.Commit.Sha.Substring (0, 6), result.Status));
			return log;
		}

		public MergeResult StalkUpTo (IReadOnlyList<Commit> commits)
		{
			var mergeFiles = new List<String> ();
			var startingCommit = head.Tip;
			var newCommits = new List<Commit> ();
			MergeResult mergeResult = null;
			var mergeOptions = new MergeOptions ();
			var signature = new Signature (userName, userEmail, DateTime.Now);
			//var checkoutOptions = new CheckoutOptions ();
//			var commitMsg = new List<String> ();

			commitMsg = (String.Format ("Automated Stalker (StalkUpTo) merge commit from: '{0}' to: '{1}' into {2}", commits.First ().Sha.Substring (0, 6), commits.Last ().Sha.Substring (0, 6), head.Tip.Sha.Substring (0, 6)));
//			Console.WriteLine ("Working on {0} commits", commits.Count);

			var commit = commits.Last ();
			Array.ForEach (CommitLog (commit, true).ToArray(), Console.WriteLine);

			mergeOptions.FastForwardStrategy = FastForwardStrategy.FastForwardOnly;
			mergeOptions.CommitOnSuccess = true;

			//checkoutOptions.CheckoutModifiers = CheckoutModifiers.None;
			try {
				Console.WriteLine ("Merge FF: {0} : {1}", commit.Sha.Substring (0,6), commit.MessageShort);
				mergeResult = repo.Merge (commit, signature, mergeOptions);
				var tmpMsg = mergeResult.Commit.Message;
				var commitOptions = new CommitOptions ();
				commitOptions.AmendPreviousCommit = true;
				repo.Commit (commitMsg + "\n\n" + tmpMsg, signature, commitOptions);
			} catch {
				Console.WriteLine ("Merge FF Failed: CheckoutFileConflictStrategy.Theirs");
				//commitMsg = CommitLog (commit, true, commitMsg);
				// this will happen as libgit2sharp throws ex if fast forward not possible
				// so lets get a commit message ready
				mergeFiles.AddRange (FilesToMerge (commit));
						
				mergeOptions.FastForwardStrategy = FastForwardStrategy.NoFastForward;
				mergeOptions.FileConflictStrategy = CheckoutFileConflictStrategy.Theirs;
				mergeOptions.CommitOnSuccess = true;
				try {
					var fgColor = Console.ForegroundColor;
					var bgColor = Console.BackgroundColor;
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine ("Merging commit : {0}", commit.Sha);
					Console.WriteLine (" - Info : {0} / {1} / {2}", commit.Author.When, commit.Author.Name, commit.Author.Email);
					Console.WriteLine (" - Msg : {0}", commit.Message);
					Console.ForegroundColor = fgColor;
					Console.BackgroundColor = bgColor;
					mergeResult = repo.Merge (commit, signature, mergeOptions);
					var tmpMsg = mergeResult.Commit.Message;
					var commitOptions = new CommitOptions ();
					commitOptions.AmendPreviousCommit = true;
					repo.Commit (commitMsg + "\n\n" + tmpMsg, signature, commitOptions);
					//Array.ForEach (MergeResultLog (mergeResult).ToArray (), Console.WriteLine);
				} catch (Exception e) {
					Console.WriteLine (e.Message);
				}
			}
			return mergeResult;
		}


		public void CheckoutOurs (Conflict conflict)
		{
			// Not available in libgit2sharp : https://github.com/libgit2/libgit2sharp/issues/868
			// git checkout --ours 
			system(String.Format ("cd {0} ; git checkout --ours {1}", repo.Info.Path, conflict.Ours.Path)); 
			repo.Stage (conflict.Ours.Path);
		}

		public void CheckoutTheirs (Conflict conflict)
		{
			// Not available in libgit2sharp : https://github.com/libgit2/libgit2sharp/issues/868
			// git checkout --theirs
			system(String.Format ("cd {0} ; git checkout --theirs {1}", repo.Info.Path, conflict.Theirs.Path)); 
			repo.Stage (conflict.Theirs.Path);
		}

		public Commit StalkerCommit ()
		{
			var signature = new Signature (userName, userEmail, DateTime.Now);
			var options = new CommitOptions ();
			options.AllowEmptyCommit = false;
			options.AmendPreviousCommit = false;
			var com = repo.Commit (commitMsg, signature, options);
			return com;
		}

		public MergeResult StalkMerge (Commit commit)
		{
			MergeResult mergeResult = null;
			var mergeOptions = new MergeOptions ();
			var signature = new Signature (userName, userEmail, DateTime.Now);

			commitMsg = String.Format ("Automated Stalker (StalkMerge) merge commit from: '{0}' into {1}", commit.Sha.Substring (0, 6), head.Tip.Sha.Substring (0, 6));
			//commitMsg = String.Format ("Stalker merge commit from: '{0}' into {1}", commit.Sha.Substring (0, 6), head.Tip.Sha.Substring (0, 6));

			mergeOptions.FastForwardStrategy = FastForwardStrategy.NoFastForward;
			mergeOptions.FileConflictStrategy = CheckoutFileConflictStrategy.Normal;
			mergeOptions.FindRenames = true;
			mergeOptions.CommitOnSuccess = false;

			try {
				var fgColor = Console.ForegroundColor;
				var bgColor = Console.BackgroundColor;
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine ("Stalker - Merging commit : {0}", commit.Sha);
				Console.WriteLine (" - Info : {0} / {1} / {2}", commit.Author.When, commit.Author.Name, commit.Author.Email);
				Console.WriteLine (" - Msg : {0}", commit.Message);
				Console.ForegroundColor = fgColor;
				Console.BackgroundColor = bgColor;

				mergeResult = repo.Merge (commit, signature, mergeOptions);
				Console.WriteLine ("Merge results: {0}", mergeResult.Status);
			} catch (Exception e) {
				if (mergeResult != null) {
					Console.WriteLine ("Merge failed: {0}", mergeResult.Status);
				} else {
					Console.WriteLine (e);
					Console.WriteLine (Environment.StackTrace);
				}
			}
			return mergeResult;
		}

		public MergeResult UpstreamSync ()
		{
			// Need to check status before doing this
			MergeResult mergeResult;
			var mergeOptions = new MergeOptions ();
			mergeOptions.FastForwardStrategy = FastForwardStrategy.FastForwardOnly;
			var signature = new Signature ("x", "x", DateTime.Now);
			var checkoutOptions = new CheckoutOptions ();
			checkoutOptions.CheckoutModifiers = CheckoutModifiers.Force;
            
			try {
				Console.WriteLine ("Checkout master");
				repo.Checkout (master);
				Console.Write ("Any Key");
				Console.ReadKey ();
				Console.WriteLine ("Fetch Upstream");
				Remote remote = repo.Network.Remotes ["upstream"];
				repo.Network.Fetch (remote);
				Console.Write ("Any Key");
				Console.ReadKey ();
				Console.WriteLine ("Merge the fetched Upstream");
				mergeResult = repo.MergeFetchedRefs (signature, mergeOptions);
				Console.WriteLine (mergeResult.Status);
				Console.Write ("Any Key");
				Console.ReadKey ();
			} finally {
				Console.WriteLine ("Checkout prior head");
				repo.Checkout (head);
				Console.Write ("Any Key");
				Console.ReadKey ();
			}
			return mergeResult;
		}

		public bool checkoutHead ()
		{
			var rval = true;
			if (repo.Head == head) {
				return rval;
			}
			var checkoutOptions = new CheckoutOptions ();
//            foreach (var sub in repo.Submodules)
//            {
//                Console.WriteLine("{0} {1} {2}", sub.Name, sub.RetrieveStatus().IsWorkingDirectoryDirty(), sub.RetrieveStatus().IsUnmodified());
//            }
			//  Note: There are issues in gitlib2/sharp in the use of .IsWorkingDirectoryDirty() and .IsUnmodified())
			//        Really should open the submodule ourself via a new repo object...
			var dirtySubmodules = repo.Submodules.Where (s => s.RetrieveStatus ().IsWorkingDirectoryDirty ());
			if (!dirtySubmodules.Any ()) {
//                Console.WriteLine("No submodules dirty, forcing checkout");
				checkoutOptions.CheckoutModifiers = CheckoutModifiers.Force;
			}
//            foreach (var item in repo.RetrieveStatus().Where(m => m.State == FileStatus.ModifiedInWorkdir))
//            {
//                Console.WriteLine("{0} : {1}", item.State, item.FilePath);
//            }
//            Console.Write("Any Key");
//            Console.ReadKey();            
//            Console.WriteLine("Checkout prior head");
			try {
				repo.Checkout (head, checkoutOptions);
//                Console.Write("Any Key"); Console.ReadKey();     
			} catch (Exception e) {
				Console.WriteLine ("HEAD checkout failed");
				Console.WriteLine (e.Message);
				//Console.WriteLine(e.StackTrace);
				rval = false;
			}
			return rval;
		}
	}
}

