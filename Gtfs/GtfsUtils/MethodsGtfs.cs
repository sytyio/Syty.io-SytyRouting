using System.Net; //download file 
using System.IO.Compression; //zip
using NLog;
using SytyRouting.Gtfs.ModelCsv;

namespace SytyRouting.Gtfs.GtfsUtils
{
public class MethodsGtfs
{

    private static Logger logger = LogManager.GetCurrentClassLogger();

    public static async Task DownloadsGtfs()
    {
        CleanGtfs();
        List<Task> listDwnld = new List<Task>();
        foreach (ProviderCsv provider in Enum.GetValues(typeof(ProviderCsv)))
        {

            listDwnld.Add(DownloadGtfs(provider));

        }
        if(listDwnld.Count == 0)
        {
            logger.Info("Nothing to download");
        }
        try
        {
            await Task.WhenAll(listDwnld);
        }
        catch (AggregateException e)
        {
            var collectedExceptions = e.InnerExceptions;
            logger.Info("Error with the download of {0} provider(s)", collectedExceptions.Count);
            foreach (var inEx in collectedExceptions)
            {
                logger.Info(inEx.Message);
            }
        }
        catch(Exception)
        {
            // Case when there is no provider.
        }
    }

    public static void CleanGtfs()
    {
        if (Directory.Exists("GtfsData"))
        {
            Directory.Delete("GtfsData", true);
            logger.Info("Cleaning GtfsData");

        }
        else
        {
            logger.Info("No data found");
        }
    }

    private static async Task DownloadGtfs(ProviderCsv provider)
    {
        string path = System.IO.Path.GetFullPath("GtfsData");

        logger.Info("Start download {0}", provider);
        string fullPathDwln = path + $"\\{provider}\\gtfs.zip";
        string fullPathExtract = path + $"\\{provider}\\gtfs";
        Uri linkOfGtfs = new Uri("https://huhu");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(path + $"\\{provider}");
        switch (provider)
        {
            case ProviderCsv.stib:
                linkOfGtfs = new Uri("https://stibmivb.opendatasoft.com/api/datasets/1.0/gtfs-files-production/alternative_exports/gtfszip/");
                break;
            case ProviderCsv.tec:
                linkOfGtfs = new Uri("https://gtfs.irail.be/tec/tec-gtfs.zip");
                break;
            case ProviderCsv.ter:
                linkOfGtfs = new Uri("https://eu.ftp.opendatasoft.com/sncf/gtfs/export-ter-gtfs-last.zip");
                break;
            case ProviderCsv.canada:
                linkOfGtfs = new Uri("https://transitfeeds.com/p/calgary-transit/238/latest/download");
                break;
            case ProviderCsv.tgv:
                linkOfGtfs = new Uri("https://eu.ftp.opendatasoft.com/sncf/gtfs/export_gtfs_voyages.zip");
                break;
            case ProviderCsv.suisse:
                linkOfGtfs = new Uri("https://opentransportdata.swiss/de/dataset/timetable-2021-gtfs2020/resource_permalink/gtfs_fp2021_2021-12-08_09-10.zip");
                break;
        }
        Task dwnldAsync;

        using (WebClient wc = new WebClient())
        {
            dwnldAsync = wc.DownloadFileTaskAsync(
                // Param1 = Link of file
                linkOfGtfs,
                // Param2 = Path to save
                fullPathDwln);
            logger.Info("downloaded directory for {0}", provider);
        }
        try
        {
            await dwnldAsync;
        }
        catch
        {
            logger.Info("Error with the provider {0}", provider);
            throw;
        }
        await Task.Run(() => ZipFile.ExtractToDirectory(fullPathDwln, fullPathExtract));
        logger.Info("{0} done", provider);

        if (Directory.Exists(fullPathExtract))
        {
            File.Delete(fullPathDwln); //delete .zip
        }
    }
}
}