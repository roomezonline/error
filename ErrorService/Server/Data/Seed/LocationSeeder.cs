using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Data.Seed;

public static class LocationSeeder
{
    private sealed class ProvinceSeedItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CitySeedItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("province_id")]
        public int ProvinceId { get; set; }
    }

    public static async Task SeedAsync(ErrorServiceDbContext db, IWebHostEnvironment env, ILogger logger, CancellationToken ct = default)
    {
        try 
        {
            logger.LogInformation("LocationSeeder: Starting...");

            var hasAnyCity = await db.Cities.AnyAsync(ct);
            if (hasAnyCity)
            {
                logger.LogInformation("LocationSeeder: Cities already populated. Skipping.");
                return;
            }

            var root = env.ContentRootPath;
            var seedDir = Path.Combine(root, "Data", "Seed");
            var provincesPath = Path.Combine(seedDir, "iran-provinces.json");
            var citiesPath = Path.Combine(seedDir, "iran-cities.json");

            if (!File.Exists(provincesPath) || !File.Exists(citiesPath))
            {
                logger.LogError("LocationSeeder: SEED FILES MISSING! {P} {C}", provincesPath, citiesPath);
                return;
            }

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var provincesRaw = JsonSerializer.Deserialize<List<ProvinceSeedItem>>(File.ReadAllText(provincesPath), jsonOptions) ?? new();
            var citiesRaw = JsonSerializer.Deserialize<List<CitySeedItem>>(File.ReadAllText(citiesPath), jsonOptions) ?? new();

            logger.LogInformation("LocationSeeder: JSON loaded. Provinces: {P}, Cities: {C}", provincesRaw.Count, citiesRaw.Count);

            // 1. Ensure Provinces
            var dbProvinces = await db.Provinces.ToListAsync(ct);
            if (!dbProvinces.Any())
            {
                logger.LogInformation("LocationSeeder: Provinces table empty. Inserting from JSON...");
                foreach(var p in provincesRaw)
                {
                    db.Provinces.Add(new Province { Name = p.Name.Trim() });
                }
                await db.SaveChangesAsync(ct);
                dbProvinces = await db.Provinces.ToListAsync(ct);
            }
            logger.LogInformation("LocationSeeder: DB now has {Count} provinces.", dbProvinces.Count);

            // 2. Map JSON Province ID to DB ID
            var provinceMap = new Dictionary<int, int>();
            foreach (var pr in provincesRaw)
            {
                var dbP = dbProvinces.FirstOrDefault(x => x.Name.Trim() == pr.Name.Trim());
                if (dbP != null) 
                {
                    provinceMap[pr.Id] = dbP.Id;
                }
            }
            logger.LogInformation("LocationSeeder: Created mapping for {Count} provinces.", provinceMap.Count);

            // 3. Prepare Cities - filter out duplicates AND district names (ending with numbers)
            logger.LogInformation("LocationSeeder: Mapping and preparing cities...");
            var citiesToInsert = new List<City>();
            var seenKeys = new HashSet<string>(); // Key = "ProvinceId|CityName"
            int duplicateCount = 0;
            int skippedCount = 0;
            int districtSkipped = 0;
            
            foreach (var c in citiesRaw)
            {
                if (!provinceMap.TryGetValue(c.ProvinceId, out var dbProvinceId))
                {
                    skippedCount++;
                    continue;
                }

                var cityName = c.Name.Trim();
                if (string.IsNullOrWhiteSpace(cityName)) 
                {
                    skippedCount++;
                    continue;
                }

                // Skip district names (names ending with numbers like "اهواز1", "تهران2", etc.)
                if (IsDistrictName(cityName))
                {
                    districtSkipped++;
                    continue;
                }

                // Check for duplicates within the same province
                var key = $"{dbProvinceId}|{cityName}";
                if (seenKeys.Contains(key))
                {
                    duplicateCount++;
                    continue; // Skip duplicate
                }
                seenKeys.Add(key);
                
                citiesToInsert.Add(new City { Name = cityName, ProvinceId = dbProvinceId });
            }

            logger.LogInformation("LocationSeeder: Prepared {Valid} cities. Skipped: {Skipped}, Duplicates: {Dupes}, Districts: {Districts}", 
                citiesToInsert.Count, skippedCount, duplicateCount, districtSkipped);

            if (citiesToInsert.Count == 0)
            {
                logger.LogError("LocationSeeder: No valid cities to insert!");
                return;
            }

            // 4. Batch Insert Cities with per-batch error handling
            logger.LogInformation("LocationSeeder: Inserting {N} cities in batches of {Batch}...", citiesToInsert.Count, 200);
            int insertedTotal = 0;
            int batchNum = 0;
            const int batchSize = 200;
            
            for (int i = 0; i < citiesToInsert.Count; i += batchSize)
            {
                batchNum++;
                var batch = citiesToInsert.Skip(i).Take(batchSize).ToList();
                
                try
                {
                    db.Cities.AddRange(batch);
                    await db.SaveChangesAsync(ct);
                    insertedTotal += batch.Count;
                    
                    if (batchNum % 2 == 0 || i + batch.Count >= citiesToInsert.Count)
                    {
                        logger.LogInformation("LocationSeeder: Batch {BatchNum} complete. Total inserted: {Count} cities...", 
                            batchNum, insertedTotal);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "LocationSeeder: FAILED on batch {BatchNum} (items {Start}-{End}). Continuing...", 
                        batchNum, i, i + batch.Count);
                    // Clear the failed entities from tracking so they don't block future batches
                    foreach (var entity in batch)
                    {
                        db.Entry(entity).State = EntityState.Detached;
                    }
                }
            }

            logger.LogInformation("LocationSeeder: COMPLETED. Inserted {Inserted}/{Expected} cities.", 
                insertedTotal, citiesToInsert.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LocationSeeder: CRITICAL ERROR during seeding process.");
        }
    }

    private static bool IsDistrictName(string cityName)
    {
        // Check if name ends with a digit (district names like "اهواز1", "تهران2", etc.)
        if (string.IsNullOrWhiteSpace(cityName) || cityName.Length < 2)
            return false;
        
        // Get the last character
        char lastChar = cityName[cityName.Length - 1];
        
        // If last char is a digit (0-9), it's likely a district name
        return char.IsDigit(lastChar);
    }
}
