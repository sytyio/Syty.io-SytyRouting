////////////////////////////////////////
// Notes on 'ways' cost calculation:  //
////////////////////////////////////////
// https://wiki.openstreetmap.org/wiki/Routing
// https://wiki.openstreetmap.org/wiki/Routing_profiles
// https://github.com/pgRouting/osm2pgrouting/issues/275
// https://workshop.pgrouting.org/2.1.0-dev/en/chapters/advanced.html#cost-manipulations
// https://postgis.net/docs/ST_Length.html
// https://postgis.net/docs/ST_SRID.html
// 
//
// https://postgis.net/workshops/postgis-intro/projection.html
// The most common SRID for geographic coordinates is 4326, which corresponds to “longitude/latitude on the WGS84 spheroid”. You can see the definition here:
// https://epsg.io/4326
//
// https://stackoverflow.com/questions/63594711/net-topologysuite-projnet-problem-with-linestring-length-calculation
// https://community.esri.com/t5/coordinate-reference-systems-blog/distance-on-a-sphere-the-haversine-formula/ba-p/902128




/////////////////////////////////
// Some tests on the database:  //
/////////////////////////////////
// PostGIS
// SELECT length, cost, the_geom, ST_Length(the_geom) as st_length FROM public.ways ORDER BY gid ASC LIMIT 100;
// SELECT ST_Length(ST_GeomFromText('LINESTRING(743238 2967416,743238 2967450,743265 2967450, 743265.625 2967416,743238 2967416)',2249));
// SELECT gid, tag_id,length, cost, reverse_cost, one_way, the_geom FROM public.ways ORDER BY gid ASC LIMIT 100;
// SELECT gid, tag_id,length, cost, reverse_cost, one_way, length - cost as diff_length_cost FROM public.ways ORDER BY gid ASC LIMIT 100;
// SELECT gid, tag_id,length, cost, reverse_cost, one_way, length - cost as diff_length_cost,
// 	case when one_way = 1 then length + reverse_cost
// 	     when one_way = 0 then length - reverse_cost
// 	end
// 	as diff_length_negOne_way_reverse_cost FROM public.ways ORDER BY gid ASC LIMIT 100;
// SELECT length - cost as diff_length_cost FROM public.ways LIMIT 100;
// SELECT gid, tag_id,length, cost, reverse_cost, one_way, length - cost as diff_length_cost,
// 	case when length - cost = 0 then 1
// 	     else 0
// 	end
// 	as diff_is_zero FROM public.ways ORDER BY gid ASC LIMIT 100;
// SELECT cost FROM public.ways WHERE cost > (
// 	SELECT cost FROM public.ways ORDER BY gid ASC LIMIT 1
// );

// SELECT gid, tag_id, length, cost, reverse_cost, one_way,
// 	length+cost as length__cost, length-reverse_cost as length__reverse_cost,
// 	ST_Length(the_geom)+cost as st_length__cost, ST_Length(the_geom)-reverse_cost as st_length__reverse_cost
// 		FROM public.ways  WHERE one_way = -1 ORDER BY gid ASC LIMIT 100;
		
// SELECT gid, tag_id, length, cost, reverse_cost, one_way,
// 	length-cost as length__cost, length-reverse_cost as length__reverse_cost,
// 	ST_Length(the_geom)-cost as st_length__cost, ST_Length(the_geom)-reverse_cost as st_length__reverse_cost
// 		FROM public.ways  WHERE one_way = 0 ORDER BY gid ASC LIMIT 100;
		
// SELECT gid, tag_id, length, cost, reverse_cost, one_way,
// 	length-cost as length__cost, length+reverse_cost as length__reverse_cost,
// 	ST_Length(the_geom)-cost as st_length__cost, ST_Length(the_geom)+reverse_cost as st_length__reverse_cost
// 		FROM public.ways  WHERE one_way = 1 ORDER BY gid ASC LIMIT 100;
		
// SELECT gid, tag_id, length, cost, reverse_cost, one_way,
// 	length-cost as length__cost, length-reverse_cost as length__reverse_cost,
// 	ST_Length(the_geom)-cost as st_length__cost, ST_Length(the_geom)-reverse_cost as st_length__reverse_cost
// 		FROM public.ways  WHERE one_way = 2 ORDER BY gid ASC LIMIT 100;
//
//
// SELECT the_geom, ST_SRID(the_geom) as srid FROM public.ways LIMIT 100;
// SELECT the_geom, ST_SRID(the_geom) as srid FROM public.ways WHERE ST_SRID(the_geom)!=4326 LIMIT 100;
//
// -- the default calculation uses a spheroid
// SELECT ST_Length(the_geog) As length_spheroid,  ST_Length(the_geog,false) As length_sphere
// FROM (SELECT ST_GeographyFromText('SRID=4326;LINESTRING(-72.1260 42.45, -72.1240 42.45666, -72.123 42.1546)') As the_geog)
//  As foo;
//
// SELECT osm_id, source, target, cost, reverse_cost, one_way,
//   x1, y1, x2, y2, source_osm, target_osm, length_m, the_geom, maxspeed_forward, maxspeed_backward,
//   length, ST_Length(the_geom) as st_length, ST_Length(the_geom,true) as st_length_spheroid, ST_Length(the_geom,false) as st_length_sphere
//   FROM public.ways where length_m is not null

//////////////////////////
// Observations:        //
//////////////////////////
// Original cost:
// one_way = -1 (Reversed),         cost = -ST_Length(the_geom);
//                          reverse_cost =  ST_Length(the_geom);
//
// one_way =  0 (Unknown) or
// one_way =  2 (No),               cost =  ST_Length(the_geom);
//                          reverse_cost =  ST_Length(the_geom);
//
// one_way =  1 (Yes),              cost =  ST_Length(the_geom);
//                          reverse_cost = -ST_Length(the_geom);
//
//
// length = the_geom.Length (using NTS)
//
// (geometry.Length = Cartesian 2D distance of the geometry CoordinateSequence)
//
//
// length_m = ST_Length(the_geom,true)
//
// ST_Length(the_geom,true) is length in meters calculatated using a spheroid (POSTGIS). For geography types computation is performed using the inverse geodesic calculation. Units of length are in meters.
