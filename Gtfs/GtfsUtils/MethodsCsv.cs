using NetTopologySuite.Geometries;
using NLog;
using CsvHelper;
using System.Globalization;
using NetTopologySuite.Operation.Distance;
using SytyRouting.Gtfs.ModelCsv;
using SytyRouting;

namespace SytyRouting.Gtfs.GtfsUtils
{
public class MethodsCsv
{

    private static Logger logger = LogManager.GetCurrentClassLogger();


    public static List<StopTimesCsv> GetAllStopTimes(ProviderCsv provider)
    {

        // stop times of chosen society 
        string fullPathTimes = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\stop_times.txt");
        try
        {
            using (var reader = new StreamReader(fullPathTimes))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    return csv.GetRecords<StopTimesCsv>().ToList();
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            logger.Info("Something went wrong with de {0} directory (missing gtfs)", provider);
            throw;
        }
    }

    public static List<ShapeCsv> GetAllShapes(ProviderCsv provider)
    {
        try
        {
            // Shapes of chosen society
            string fullPathShape = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\shapes.txt");
            using (var reader = new StreamReader(fullPathShape))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    return csv.GetRecords<ShapeCsv>().ToList();
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            logger.Info("Something went wrong with de {0} directory (missing gtfs)", provider);
            throw;
        }
        catch (FileNotFoundException)
        {
            logger.Info("No given shapes (file empty or not present)");
            return new List<ShapeCsv>();
        }
    }

    public static List<StopCsv> GetAllStops(ProviderCsv provider)
    {
        // stops of chosen society
        string pathStop = $"GtfsData\\{provider}\\gtfs\\stops.txt";
        string fullPathStop = System.IO.Path.GetFullPath(pathStop);
        try
        {
            using (var reader = new StreamReader(fullPathStop))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    return csv.GetRecords<StopCsv>().ToList();
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            logger.Info("Something went wrong with de {0} directory (missing gtfs)", provider);
            throw;
        }
    }

    public static List<RouteCsv> GetAllRoutes(ProviderCsv provider)
    {
        // routes of chosen society
        string fullPathRoute = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\routes.txt");
        try
        {
            using (var reader = new StreamReader(fullPathRoute))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    return csv.GetRecords<RouteCsv>().ToList();
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            logger.Info("Something went wrong with de {0} directory (missing gtfs)", provider);
            throw;
        }
    }

    public static List<double[]> ListOfPointsToListOfDistance(List<Point> pointsForOneTrip)
    {
        List<double[]> distances = new List<double[]>();
        for (int i = 0; i < pointsForOneTrip.Count - 1; i++)
        {
            double[] arrayOfDistances = { DistanceBetweenTwoPoint(pointsForOneTrip[i], pointsForOneTrip[i + 1]) };
            distances.Add(arrayOfDistances);
        }
        return distances;
    }


    public static double DistanceBetweenTwoPoint(Point point1, Point point2)
    {
        return Helper.GetDistance(point1.X, point1.Y, point2.X, point2.Y);
    }

    public static double[] DistancesBetweenTwoPointNearestLineString
        (Point stop1, Point stop2, string shapeId, List<ShapeCsv> recordsShape)
    {
        if (recordsShape.Count == 0)
        {
            logger.Info("No shapes available");
            throw new Exception("No shapes available ");
        }
        LineString lineString = CreateLineString(recordsShape, shapeId);
        // logger.Info(lineString);
        Coordinate[] coordinateA = DistanceOp.NearestPoints(lineString, stop1);
        Coordinate[] coordinateB = DistanceOp.NearestPoints(lineString, stop2);
        Point stop1OnLineString = new Point(coordinateA[0]);
        Point stop2ONLineString = new Point(coordinateB[0]);
        double[] arrayOfDistances = new double[4];
        /**
            0 : between two stops
            1 : between stop1 and linestring
            2 : between stop2 and linestring
            3 : between the two points on linestring
        */
        arrayOfDistances[0] = Helper.GetDistance(stop1.X, stop1.Y, stop2.X, stop2.Y);
        arrayOfDistances[1] = Helper.GetDistance(stop1.X, stop1.Y, stop1OnLineString.X, stop1OnLineString.Y);
        arrayOfDistances[2] = Helper.GetDistance(stop2.X, stop2.Y, stop2ONLineString.X, stop2ONLineString.Y);
        arrayOfDistances[3] = Helper.GetDistance(stop1OnLineString.X, stop1OnLineString.Y, stop2ONLineString.X, stop2ONLineString.Y);
        return arrayOfDistances;
    }

    public static List<TripCsv> GetAllTrips(ProviderCsv provider)
    {
        // Trips of chosen society
        string fullPathTrip = System.IO.Path.GetFullPath($"GtfsData\\{provider}\\gtfs\\trips.txt");
        try
        {
            using (var reader = new StreamReader(fullPathTrip))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    return csv.GetRecords<TripCsv>().ToList();
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            logger.Info("Something went wrong with de {0} directory (missing gtfs)", provider);
            throw;
        }
    }

    public static List<double> ListOfStopsTimeToListOfTimes(List<StopTimesCsv> listStopsTime)
    {
        int size = listStopsTime.Count;
        List<double> allTimes = new List<double>();
        for (int i = 0; i < size - 1; i++)
        {
            allTimes.Add(TimeBetweenTwoStops(listStopsTime[i], listStopsTime[i + 1]));
        }
        return allTimes;
    }

    public static double TimeBetweenTwoStops(StopTimesCsv departureStop, StopTimesCsv arrivalStop)
    {
        TimeSpan departureTimeStop1;
        TimeSpan arrivalTimeStop2;
        try
        {
            departureTimeStop1 = TimeSpan.Parse(departureStop.DepartureTime!);
        }
        catch (System.OverflowException)
        {
            departureTimeStop1 = ParseMore24Hours(departureStop.DepartureTime!);
        }
        try
        {
            arrivalTimeStop2 = TimeSpan.Parse(arrivalStop.ArrivalTime!);
        }
        catch (System.OverflowException)
        {
            arrivalTimeStop2 = ParseMore24Hours(arrivalStop.ArrivalTime!);
        }
        double time = (arrivalTimeStop2 - departureTimeStop1).TotalSeconds;
        //   logger.Info("Heure départ {0}, heure arrivée {1}, Temps mis {2}",departureTimeStop1,arrivalTimeStop2, time);
        return time;
    }

    public static TimeSpan ParseMore24Hours(string timeToParse)
    {
        string[] split = timeToParse.Split(":");
        int hour = Int16.Parse(split[0]);
        int min = Int16.Parse(split[1]);
        int seconds = Int16.Parse(split[2]);
        return new TimeSpan(hour % 24, min, seconds);
    }

    public static LineString CreateLineString(List<ShapeCsv> recordsShape, string shapeId)
    {

        var shapeInfos = recordsShape.FindAll(x => x.Id == shapeId);
        // CREATION D UN LINESTRING
        Coordinate[] arrayOfCoordinate = new Coordinate[shapeInfos.Count];
        for (int i = 0; i < shapeInfos.Count; i++)
        {
            ShapeCsv shape = shapeInfos[i];
            Coordinate coordinate = new Coordinate(shape.PtLat, shape.PtLon);
            arrayOfCoordinate[i] = coordinate;
        }
        LineString lineString = new LineString(arrayOfCoordinate);
        return lineString;
    }

    /**Returns a list of arrays of 1 or 2 double
        If there is a shape:
            - the first double is the distance between the two points nearest on the linestring for the two stops
            - the second double is the distance between the first stop and the nearest point on the linestring
           
        If there is no shape : 
            - the only double is the distance between the two stops

     */
    public static List<double[]> GetAllDistancesForOnTrip(List<ShapeCsv> recordsShape,
                                                        List<Point> pointsForOneTrip,
                                                        TripCsv chosenTripForChosenRoute
                                                        )
    {
        List<double[]> distancesForOneTrip = new List<double[]>();
        // If there is no given shape, calculate the distance between 2 stops based on their coordinates
        if (recordsShape.Count == 0)
        {
            logger.Info("No shapes available");
            return ListOfPointsToListOfDistance(pointsForOneTrip);
        }
        // If there is a shape, calculate the distance between 2 points of the shape
        //  These two points are the closest to two consecutive stops
        else
        {
            LineString lineString = CreateLineString(recordsShape, chosenTripForChosenRoute.ShapeId);
            for (int i = 0; i < pointsForOneTrip.Count - 1; i++)
            {
                Coordinate[] coordinateA = DistanceOp.NearestPoints(lineString, pointsForOneTrip[i]);
                Coordinate[] coordinateB = DistanceOp.NearestPoints(lineString, pointsForOneTrip[i + 1]);
                Point pointA = new Point(coordinateA[0]);
                Point pointB = new Point(coordinateB[0]);
                double[] arrayOfDistances = new double[2];

                arrayOfDistances[0] = Helper.GetDistance(pointA.X, pointA.Y, pointB.X, pointB.Y);
                arrayOfDistances[1] = Helper.GetDistance(pointA.X, pointA.Y, pointsForOneTrip[i].X, pointsForOneTrip[i].Y);
                distancesForOneTrip.Add(arrayOfDistances);
                logger.Info("Infos : ");
                logger.Info("the distance between the first stop and the nearest point on the linestring {0}", distancesForOneTrip[i][1]);
                logger.Info("Distance between the two nearest point on linestring {0}", distancesForOneTrip[i][0]);
                logger.Info("Distance between the 2 intial stops {0}", Helper.GetDistance(pointsForOneTrip[i].X, pointsForOneTrip[i].Y, pointsForOneTrip[i + 1].X, pointsForOneTrip[i + 1].Y));
            }
            return distancesForOneTrip;
        }
    }


    public static void PrintArray(object[] array)
    {
        for (int i = 0; i < array.Count(); i++)
        {
            logger.Info(array[i]);
        }
    }

    public static void printDistinctShapesForOneTrip(List<TripCsv> recordsTrip, RouteCsv chosenRoute)
    {
        var nbParRoute = recordsTrip.FindAll(x => x.RouteId == chosenRoute.Id).GroupBy(x => x.ShapeId).Select(x => Tuple.Create(x.Key, x.Count()));
        foreach (var item in nbParRoute)
        {
            logger.Info("shape_id {0}, number of use {1}", item.Item1, item.Item2);
        }

        var tripsForChosenRoute = recordsTrip.FindAll(x => x.RouteId == chosenRoute.Id);
        logger.Info("Number of distinct trip for one route {0}", tripsForChosenRoute.Count());
        logger.Info("Id of the chosen route {0} and name {1}", chosenRoute.Id, chosenRoute.LongName);
    }
}
}
