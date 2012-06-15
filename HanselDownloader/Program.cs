using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HanselDownloader
{
	class Program
	{
		private static readonly Uri Mp3Feed = new Uri(@"http://feeds.feedburner.com/HanselminutesCompleteMP3");

		static void Main(string[] args)
		{
			
			//Task<string> stringAsync = client.GetStringAsync(Mp3Feed);

			var xDocument = XDocument.Load(Mp3Feed.AbsoluteUri);
			//var xDocument = XDocument.Load(@"c:\temp\feed.xml");

			XNamespace feedBurner = "http://rssnamespace.org/feedburner/ext/1.0";

			var showIdRegex = new Regex(@"_(\d+)");
				
			var shows = from item in xDocument.Descendants("item")
						let mp3LinkElem = item.Descendants(feedBurner + "origEnclosureLink").Single()
						select new
						{
							Mp3Uri =  new Uri(mp3LinkElem.Value),
							Id = int.Parse(showIdRegex.Matches(mp3LinkElem.Value)[0].Groups[1].ToString())
				        };

			var showsToDownload = shows.Where(x => x.Id <= 100);

			ParallelOptions po = new ParallelOptions
			                     	{
			                     		MaxDegreeOfParallelism = 5
			                     	};
			Parallel.ForEach(showsToDownload, po, show =>
				{
					try
					{
						using (var httpClient = new HttpClient())
						{
							var downloadStream = httpClient.GetStreamAsync(show.Mp3Uri).Result;

							var fileLocation = string.Format(@"c:\temp\shows\Hanselminutes_{0}.mp3", show.Id);
							using (downloadStream)
							using (var fileStream = File.Create(fileLocation))
							{
								Console.WriteLine(string.Format("Downloading show {0}", show.Id));
								downloadStream.CopyTo(fileStream);
							}

							Console.WriteLine(string.Format("Show {0} downloaded to {1}", show.Id, fileLocation));
						}
					}
					catch (Exception e)
					{
						Console.Error.WriteLine(e);
					}
				});

			Console.WriteLine("Done......");
			Console.ReadKey();
		}
	}
}
