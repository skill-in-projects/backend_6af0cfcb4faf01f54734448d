using Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly string _connectionString;

    // The first jobs shown on Find work. They are saved to the database when the Jobs table is created.
    private static readonly Job[] SampleJobs =
    [
        new() { Title = "Barista", Company = "Harbor Coffee Co.", Location = "Downtown", Type = "Part-time", Pay = "$18/hr", Description = "Make drinks, keep the bar clean and greet regulars. Morning shifts available." },
        new() { Title = "Delivery Driver", Company = "Pico Logistics", Location = "North Side", Type = "Full-time", Pay = "$22/hr", Description = "Deliver parcels within the city. Own car and clean licence required." },
        new() { Title = "Garden Helper", Company = "Atlas Garden Care", Location = "Westview", Type = "Contract", Pay = "$20/hr", Description = "Help with lawn care, planting and seasonal clean-ups for home gardens." },
        new() { Title = "Front Desk Assistant", Company = "Greenleaf Clinic", Location = "Central", Type = "Part-time", Pay = "$19/hr", Description = "Check in patients, answer calls and keep the waiting room tidy." },
        new() { Title = "Web Designer", Company = "Lumen Print House", Location = "Remote", Type = "Contract", Pay = "$35/hr", Description = "Design simple landing pages and print layouts for small business clients." },
    ];

    public JobsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    // GET: api/jobs
    [HttpGet]
    public async Task<ActionResult<List<Job>>> GetJobs()
    {
        var jobs = new List<Job>();
        using var conn = await OpenConnectionAsync();
        await EnsureTableAsync(conn);

        using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Title\", \"Company\", \"Location\", \"Type\", \"Pay\", \"Description\" FROM \"Jobs\" ORDER BY \"Id\" DESC", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            jobs.Add(new Job
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Company = reader.GetString(2),
                Location = reader.GetString(3),
                Type = reader.GetString(4),
                Pay = reader.GetString(5),
                Description = reader.GetString(6)
            });
        }
        return Ok(jobs);
    }

    // POST: api/jobs
    [HttpPost]
    public async Task<ActionResult<Job>> PostJob([FromBody] Job job)
    {
        using var conn = await OpenConnectionAsync();
        await EnsureTableAsync(conn);

        using var cmd = new NpgsqlCommand("INSERT INTO \"Jobs\" (\"Title\", \"Company\", \"Location\", \"Type\", \"Pay\", \"Description\") VALUES (@title, @company, @location, @type, @pay, @description) RETURNING \"Id\"", conn);
        AddJobParameters(cmd, job);
        job.Id = (int)(await cmd.ExecuteScalarAsync())!;
        return Ok(job);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Same as the other controllers: the database role has a restricted search_path.
        using var setPathCmd = new NpgsqlCommand("SET search_path = public, \"" + "$" + "user\";", conn);
        await setPathCmd.ExecuteNonQueryAsync();
        return conn;
    }

    // Creates the Jobs table with the sample jobs the first time it is missing.
    private static async Task EnsureTableAsync(NpgsqlConnection conn)
    {
        using var checkCmd = new NpgsqlCommand("SELECT to_regclass('\"Jobs\"') IS NOT NULL", conn);
        if ((bool)(await checkCmd.ExecuteScalarAsync())!) return;

        using (var createCmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS \"Jobs\" (" +
            "\"Id\" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, " +
            "\"Title\" varchar(200) NOT NULL, " +
            "\"Company\" varchar(200) NOT NULL, " +
            "\"Location\" varchar(200) NOT NULL, " +
            "\"Type\" varchar(50) NOT NULL, " +
            "\"Pay\" varchar(50) NOT NULL, " +
            "\"Description\" varchar(2000) NOT NULL)", conn))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        foreach (var sample in SampleJobs)
        {
            using var insertCmd = new NpgsqlCommand("INSERT INTO \"Jobs\" (\"Title\", \"Company\", \"Location\", \"Type\", \"Pay\", \"Description\") VALUES (@title, @company, @location, @type, @pay, @description)", conn);
            AddJobParameters(insertCmd, sample);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static void AddJobParameters(NpgsqlCommand cmd, Job job)
    {
        cmd.Parameters.AddWithValue("title", job.Title);
        cmd.Parameters.AddWithValue("company", job.Company);
        cmd.Parameters.AddWithValue("location", job.Location);
        cmd.Parameters.AddWithValue("type", job.Type);
        cmd.Parameters.AddWithValue("pay", job.Pay);
        cmd.Parameters.AddWithValue("description", job.Description);
    }
}
