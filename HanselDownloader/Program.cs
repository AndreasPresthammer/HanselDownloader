using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HanselDownloader
{
	class Program
	{
		private static readonly XNamespace FeedBurnerNamespace = @"http://rssnamespace.org/feedburner/ext/1.0";
		private static readonly Uri Mp3Feed = new Uri(@"http://feeds.feedburner.com/HanselminutesCompleteMP3");
		private static readonly Regex ShowIdRegex;
		private static int _minShowId;
		private static int _maxShowId;
		private static DirectoryInfo _saveDirectory;
		private const int MaxSimultaneousDownloads = 4;

		static Program()
		{
			ShowIdRegex = new Regex(@"_(\d+)");
		}

		static void Main(string[] args)
		{
			try
			{
				SetupArgs(args);

				var allShows = GetShows();

				var showsToDownload = (from show in allShows
				                       where
				                       	show.Id >= _minShowId &&
				                       	show.Id <= _maxShowId
				                       select show).ToList();

				DownloadInParallel(showsToDownload);

				Console.WriteLine(string.Format("Finished downloading {0} shows to {1}", showsToDownload.Count(), _saveDirectory));
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
			}
		}

		private static void DownloadInParallel(IEnumerable<Show> showsToDownload)
		{
			var po = new ParallelOptions {MaxDegreeOfParallelism = MaxSimultaneousDownloads};
			Parallel.ForEach(showsToDownload, po, show =>
				{
					try
					{
						using (var httpClient = new HttpClient())
						{
							var downloadStream = httpClient.GetStreamAsync(show.Mp3Uri).Result;

							var file = new FileInfo(_saveDirectory + string.Format(@"\Hanselminutes_{0}.mp3", show.Id));
							using (downloadStream)
							using (var fileStream = file.Create())
							{
								Console.WriteLine(string.Format("Downloading show {0}", show.Id));
								downloadStream.CopyTo(fileStream);
							}

							Console.WriteLine(string.Format("Show {0} downloaded to {1}", show.Id, file));
						}
					}
					catch (Exception e)
					{
						Console.Error.WriteLine(e);
					}
				});
		}

		private static void SetupArgs(string[] args)
		{
			if (!int.TryParse(args.ElementAtOrDefault(0), out _minShowId))
				throw new ArgumentException("First argument is the minimum show id");

			if (!int.TryParse(args.ElementAtOrDefault(1), out _maxShowId))
				throw new ArgumentException("Second argument is the maximum show id");

			var thirdArg = args.ElementAtOrDefault(2);
			if (thirdArg == null)
				throw new ArgumentException("Third argument is the target download directory");
			_saveDirectory = new DirectoryInfo(thirdArg);
			if (!_saveDirectory.Exists)
				throw new ArgumentException("Save directory does not exist");
		}

		private static IEnumerable<Show> GetShows()
		{
			var xDocument = LoadXmlFeed();

			return from item in xDocument.Descendants("item")
			       let mp3LinkElem = item.Descendants(FeedBurnerNamespace + "origEnclosureLink").Single()
			       select new Show
			              	{
			              		Id = int.Parse(ShowIdRegex.Matches(mp3LinkElem.Value)[0].Groups[1].ToString()),
			              		Mp3Uri = new Uri(mp3LinkElem.Value)
			              	};
		}

		private static XDocument LoadXmlFeed()
		{
			return XDocument.Load(Mp3Feed.AbsoluteUri);
		}
	}
}
