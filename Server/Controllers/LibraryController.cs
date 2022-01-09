namespace FileFlows.Server.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using FileFlows.Server.Helpers;
    using FileFlows.Shared.Models;
    using FileFlows.Server.Models;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Library controller
    /// </summary>
    [Route("/api/library")]
    public class LibraryController : ControllerStore<Library>
    {
        internal override async Task<List<Library>> GetDataList()
        {
            return (await GetData()).Values.Select(x =>
            {
                if (string.IsNullOrEmpty(x.Schedule))
                    x.Schedule = new string('1', 672);
                return x;
            }).ToList();
        }

        /// <summary>
        /// Gets all libraries in the system
        /// </summary>
        /// <returns>a list of all libraries</returns>
        [HttpGet]
        public async Task<IEnumerable<Library>> GetAll() => (await GetDataList()).OrderBy(x => x.Name);


        /// <summary>
        /// Get a library
        /// </summary>
        /// <param name="uid">The UID of the library</param>
        /// <returns>the library instance</returns>
        [HttpGet("{uid}")]
        public Task<Library> Get(Guid uid) => GetByUid(uid);

        /// <summary>
        /// Saves a library
        /// </summary>
        /// <param name="library">The library to save</param>
        /// <returns>the saved library instance</returns>
        [HttpPost]
        public Task<Library> Save([FromBody] Library library)
        {
            if (library?.Flow == null)
                throw new Exception("ErrorMessages.NoFlowSpecified");
            if (library.Uid == Guid.Empty)
                library.LastScanned = DateTime.MinValue; // never scanned
            if (Regex.IsMatch(library.Schedule, "^[01]{672}$") == false)
                library.Schedule = new string('1', 672);

            return base.Update(library, checkDuplicateName: true);
        }

        /// <summary>
        /// Set the enable state for a library
        /// </summary>
        /// <param name="uid">The UID of the library</param>
        /// <param name="enable">true if enabled, otherwise false</param>
        /// <returns>the updated library instance</returns>
        [HttpPut("state/{uid}")]
        public async Task<Library> SetState([FromRoute] Guid uid, [FromQuery] bool enable)
        {
            var library = await GetByUid(uid);
            if (library == null)
                throw new Exception("Library not found.");
            if (library.Enabled != enable)
            {
                library.Enabled = enable;
                return await Update(library);
            }
            return library;
        }

        /// <summary>
        /// Delete libraries from the system
        /// </summary>
        /// <param name="model">A reference model containing UIDs to delete</param>
        /// <returns>an awaited task</returns>
        [HttpDelete]
        public Task Delete([FromBody] ReferenceModel model) => DeleteAll(model);

        /// <summary>
        /// Rescans libraries
        /// </summary>
        /// <param name="model">A reference model containing UIDs to rescan</param>
        /// <returns>an awaited task</returns>
        [HttpPut("rescan")]
        public async Task Rescan([FromBody] ReferenceModel model)
        {
            foreach(var uid in model.Uids)
            {
                var item = await GetByUid(uid);
                if (item == null)
                    continue;
                item.LastScanned = DateTime.MinValue;
                await Update(item);

            }
        }

        internal async Task UpdateFlowName(Guid uid, string name)
        {
            var libraries = await GetDataList();
            foreach (var lib in libraries.Where(x => x.Flow?.Uid == uid))
            {
                lib.Flow.Name = name;
                await Update(lib);
            }
        }

        internal async Task UpdateLastScanned(Guid uid)
        {
            var lib = await GetByUid(uid);
            if (lib == null)
                return;
            lib.LastScanned = DateTime.UtcNow;
            await Update(lib);
        }


        private FileInfo[] GetTemplateFiles() => new System.IO.DirectoryInfo("Templates/LibraryTemplates").GetFiles("*.json");

        /// <summary>
        /// Gets a list of library templates
        /// </summary>
        /// <returns>a list of library templates</returns>
        [HttpGet("templates")]
        public Dictionary<string, List<Library>> GetTemplates()
        {
            Dictionary<string, List<Library>> templates = new();
            templates.Add(string.Empty, new List<Library>());
            foreach (var tf in GetTemplateFiles())
            {
                try
                {
                    string json = System.IO.File.ReadAllText(tf.FullName);
                    json = TemplateHelper.ReplaceWindowsPathIfWindows(json);
                    var jsTemplates = System.Text.Json.JsonSerializer.Deserialize<LibraryTemplate[]>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true
                    });
                    foreach(var jst in jsTemplates ?? new LibraryTemplate[] { })
                    {
                        string group = jst.Group ?? string.Empty;
                        if (templates.ContainsKey(group) == false)
                            templates.Add(group, new List<Library>());
                        templates[group].Add(new Library
                        {
                            Enabled = true,
                            FileSizeDetectionInterval = jst.FileSizeDetectionInterval,
                            Filter = jst.Filter ?? string.Empty,
                            Name = jst.Name,
                            Description = jst.Description,
                            Path = jst.Path,
                            Priority = jst.Priority,
                            ScanInterval = jst.ScanInterval
                        });
                    }
                }
                catch (Exception) { }
            }
            return templates;
        }
    }
}