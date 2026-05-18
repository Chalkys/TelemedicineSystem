using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.Core.Entities;

namespace TelemedicineSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SeedController(AppDbContext context)
        {
            _context = context;
        }

        // DTO для десериализации
        public class DiseaseSeedDto
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        public class MedicationSeedDto
        {
            public string Name { get; set; }
            public string Dosage { get; set; }
            public string Frequency { get; set; }
        }

        [HttpPost("mkb")]
        public async Task<IActionResult> SeedMkb()
        {
            var json = await System.IO.File.ReadAllTextAsync(
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "mkb_codes.json"));
            var items = JsonSerializer.Deserialize<List<DiseaseSeedDto>>(json);

            foreach (var item in items)
            {
                if (!await _context.Diseases.AnyAsync(d => d.MkbCode == item.Code))
                {
                    _context.Diseases.Add(new Disease
                    {
                        DiseaseId = Guid.NewGuid(),
                        MkbCode = item.Code,
                        Name = item.Name
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Загружено {items.Count} кодов МКБ-10" });
        }

        [HttpPost("medications")]
        public async Task<IActionResult> SeedMedications()
        {
            var json = await System.IO.File.ReadAllTextAsync(
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "medications.json"));
            var items = JsonSerializer.Deserialize<List<MedicationSeedDto>>(json);

            foreach (var item in items)
            {
                if (!await _context.Medications.AnyAsync(m => m.Name == item.Name))
                {
                    _context.Medications.Add(new Medication
                    {
                        MedicationId = Guid.NewGuid(),
                        Name = item.Name,
                        Dosage = item.Dosage,
                        Frequency = item.Frequency
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Загружено {items.Count} медикаментов" });
        }
    }
}