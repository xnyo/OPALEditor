using System.Globalization;
using CommandLine;
using CsvHelper;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace RegionsCSV
{
    public class OPALObject
    {
        public string EditorID { get; set; } = "";
        public float RotationX { get; set; } = 0.0f;
        public float RotationXVariance { get; set; } = 0.0f;
        public float RotationY { get; set; } = 0.0f;
        public float RotationYVariance { get; set; } = 0.0f;

        public float RotationZ { get; set; } = 0.0f;
        public float RotationZVariance { get; set; } = 0.0f;
        public float Sink { get; set; } = 0.0f;
        public float SinkVariance { get; set; } = 0.0f;
        public float Scale { get; set; } = 0.0f;
        public float ScaleVariance { get; set; } = 0.0f;
        public bool ConformToSlope { get; set; } = false;

        public EncodableOPALObject ToEncodable(FormKey fk) => new(this, fk);
    }

    public class EncodableOPALObject : OPALObject
    {
        public FormKey FormKey { get; set; } = FormKey.Null;

        public EncodableOPALObject(OPALObject o, FormKey fk)
        {
            EditorID = o.EditorID;
            RotationX = o.RotationX;
            RotationXVariance = o.RotationXVariance;
            RotationY = o.RotationY;
            RotationYVariance = o.RotationYVariance;
            RotationZ = o.RotationZ;
            RotationZVariance = o.RotationZVariance;
            Sink = o.Sink;
            SinkVariance = o.SinkVariance;
            Scale = o.Scale;
            ScaleVariance = o.ScaleVariance;
            ConformToSlope = o.ConformToSlope;

            FormKey = fk;
        }

        public void EncodeBinary(BinaryWriter w)
        {
            w.Write(EditorID.Length);
            // Cannot write string directly to the BinaryWriter, or ULEB128
            // is prepended to the string (the ripple lore has always been proven useful so far)
            w.Write(System.Text.Encoding.ASCII.GetBytes(EditorID));
            w.Write((byte)0); // null terminator (probably)
            w.Write(FormKey.ID);
            w.Write(ConformToSlope ? 1 : 0);
            w.Write(Sink);
            w.Write(SinkVariance);
            w.Write(Scale);
            w.Write(ScaleVariance);
            w.Write(Deg2Rad(RotationX));
            w.Write(Deg2Rad(RotationXVariance));
            w.Write(Deg2Rad(RotationY));
            w.Write(Deg2Rad(RotationYVariance));
            w.Write(Deg2Rad(RotationZ));
            w.Write(Deg2Rad(RotationZVariance));
        }

        public static float Deg2Rad(float deg) => deg * (float)Math.PI / 180.0f;
    }

    public class EncodableOPAL
    {
        public List<EncodableOPALObject> Objects { get; set; } = new();

        public void EncodeBinary(BinaryWriter w)
        {
            w.Write(0); // unknown, always 0?
            w.Write(Objects.Count);
            foreach (var record in Objects)
                record.EncodeBinary(w);
        }
    }

    public class Options
    {
        [Value(
            0,
            MetaName = "FileName",
            Required = true,
            HelpText = "Input CSV file",
            Default = ""
        )]
        public string FileName { get; set; } = "";

        [Option(
            'o',
            "outputfolder",
            Required = false,
            HelpText = "Output folder (defaults to cwd)",
            Default = ""
        )]
        public string OutputFolder { get; set; } = "";

        [Option(
            'v',
            "verbose",
            Required = false,
            HelpText = "Prints all messages to standard output.",
            Default = false
        )]
        public bool Verbose { get; set; } = false;
    }

    public class Logger
    {
        public bool Verbose { get; set; } = false;

#pragma warning disable
        public void Log(string message) => Console.WriteLine(message);

        public void LogVerbose(string message)
        {
            if (!Verbose)
                return;
            Log(message);
        }
    }

    public class Program
    {
        public static void CreateOPAL(Options opts)
        {
            var logger = new Logger { Verbose = opts.Verbose };

            var outputFileName = Path.Combine(
                opts.OutputFolder,
                $"{Path.GetFileNameWithoutExtension(opts.FileName)}.opl"
            );
            logger.Log($"Exporting {opts.FileName} to {outputFileName}...");

            List<OPALObject> records;
            using var reader = new StreamReader(opts.FileName);
            {
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                records = csv.GetRecords<OPALObject>().ToList();
            }
            var wantedFormKeys = records.Select(x => x.EditorID.ToLower()).ToHashSet();
            var formKeys = new Dictionary<string, FormKey>();
            using (
                var env = GameEnvironment.Typical
                    .Builder<ISkyrimMod, ISkyrimModGetter>(GameRelease.SkyrimSE)
                    .WithLoadOrder(Implicits.BaseMasters.Skyrim(SkyrimRelease.SkyrimSE).ToArray())
                    .Build()
            )
            {
                foreach (
                    var r in env.LoadOrder.PriorityOrder
                        .SkyrimMajorRecord()
                        .WinningOverrides()
                        .Where(x => wantedFormKeys.Contains((x?.EditorID ?? "").ToLower()))
                )
                {
                    if (r.EditorID == null)
                        continue;
                    logger.LogVerbose($"Found {r.EditorID} -> {r.FormKey.ID:X8}");
                    formKeys[r.EditorID.ToLower()] = r.FormKey;
                    if (formKeys.Count >= records.Count)
                        break;
                }
            }
            using var w = new BinaryWriter(File.Open(outputFileName, FileMode.Create));
            new EncodableOPAL
            {
                Objects = records
                    .Select(x => x.ToEncodable(formKeys[x.EditorID.ToLower()]))
                    .ToList()
            }.EncodeBinary(w);
        }

        public static void Main(string[] args)
        {
            try
            {
                Parser.Default.ParseArguments<Options>(args).WithParsed(CreateOPAL);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                System.Environment.Exit(1);
            }
        }
    }
}
