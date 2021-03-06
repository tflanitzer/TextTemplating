﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using TextTemplating.Infrastructure;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.PlatformAbstractions;

namespace TextTemplating
{
    public class Program
    {
        private readonly IApplicationEnvironment _appEnv;
        private readonly ILibraryExporter _libraryExporter;

        public Program(IApplicationEnvironment appEnv, ILibraryExporter libraryExporter)
        {
            _appEnv = appEnv;
            _libraryExporter = libraryExporter;
        }

        public int Main(string[] args)
        {
            var app = new CommandLineApplication();

            var razor = app.Option("-r|--razor", "Process cshtml razor files and generate outputs.", CommandOptionType.NoValue);
            var t4 = app.Option("-t4|--t4-template", "Process t4 template files and generate outputs.", CommandOptionType.NoValue);

            var preprocess = app.Option("-p|--preprocess", "Create only a run-time text template from t4 templates (preprocessed class with TransformText method)", CommandOptionType.NoValue);
            var dir = app.Option("-d|--dir", "Processing root directory", CommandOptionType.SingleValue);

            app.OnExecute(() => ProcessTemplates(razor.HasValue(), t4.HasValue(), preprocess.HasValue(), dir.HasValue() ? dir.Values[0] : null));

            return app.Execute(args);
        }

        private async Task<int> ProcessTemplates(bool razor, bool t4, bool preprocess, string dir)
        {
            if (razor == false && t4 == false)
            {
                throw new InvalidOperationException("Set -r|--razor or -t4|--t4-template option!");
            }

            if (t4)
            {
                ProcessT4Templates(dir, preprocess);
            }
            if (razor)
            {
                await ProcessRazorTemplates(dir, preprocess);
            }

            return 0;
        }

        private void ProcessT4Templates(string dir, bool onlyPreprocess)
        {
            var templates = GetT4TemplatePaths(dir);
            foreach (var path in templates)
            {
                var host = new CommandLineEngineHost(path);

                var transformedText = ProcessT4Template(path, onlyPreprocess, host);

                var output = Path.ChangeExtension(path, host.FileExtension);
                WriteTemplate(output, transformedText, host);
            }
        }

        private string ProcessT4Template(string path, bool onlyPreprocess, CommandLineEngineHost host)
        {
            Console.WriteLine("Processing '{0}'...", path);

            var engine = new Engine(_libraryExporter, host);

            var fileName = Path.GetFileNameWithoutExtension(path);
            var content = File.ReadAllText(path);

            string transformedText;
            if (onlyPreprocess)
            {
                var relativeDir = Path.GetDirectoryName(path).Substring(_appEnv.ApplicationBasePath.Length);
                var classNamespace = _appEnv.ApplicationName;
                if (relativeDir.Length != 0)
                {
                    classNamespace += '.' + relativeDir.Replace(Path.DirectorySeparatorChar, '.');
                }

                transformedText = engine.PreprocessT4Template(content, fileName, classNamespace).PreprocessedContent;
            }
            else
            {
                transformedText = engine.ProcessT4Template(content);
            }

            return transformedText;
        }

        private async Task ProcessRazorTemplates(string dir, bool onlyPreprocess)
        {
            var templates = GetRazorTemplatePaths(dir);
            foreach (var path in templates)
            {
                var host = new CommandLineEngineHost(path);

                var transformedText = await ProcessRazorTemplate(path, onlyPreprocess, host);

                var output = path + host.FileExtension;
                WriteTemplate(output, transformedText, host);
            }
        }

        private async Task<string> ProcessRazorTemplate(string path, bool onlyPreprocess, CommandLineEngineHost host)
        {
            Console.WriteLine("Processing '{0}'...", path);

            var engine = new Engine(_libraryExporter, host);

            var fileName = Path.GetFileNameWithoutExtension(path);
            var content = File.ReadAllText(path);

            string transformedText;
            if (onlyPreprocess)
            {
                var relativeDir = Path.GetDirectoryName(path).Substring(_appEnv.ApplicationBasePath.Length);
                var classNamespace = _appEnv.ApplicationName;
                if (relativeDir.Length != 0)
                {
                    classNamespace += '.' + relativeDir.Replace(Path.DirectorySeparatorChar, '.');
                }

                transformedText = engine.PreprocessRazorTemplate(content, fileName, classNamespace).PreprocessedContent;
            }
            else
            {
                transformedText = await engine.ProcessRazorTemplate(content);
            }

            return transformedText;
        }

        private void WriteTemplate(string path, string transformedText, CommandLineEngineHost host)
        {
            Console.WriteLine("Writing '{0}'...", path);

            if (host.Encoding != null)
            {
                File.WriteAllText(path, transformedText, host.Encoding);
            }
            else
            {
                File.WriteAllText(path, transformedText);
            }
        }

        private IEnumerable<string> GetT4TemplatePaths(string dir)
        {
            var matcher = new Matcher();
            matcher.AddInclude(@"**\*.tt");

            var templates = matcher.GetResultsInFullPath(dir ?? _appEnv.ApplicationBasePath);

            return templates;
        }

        private IEnumerable<string> GetRazorTemplatePaths(string dir)
        {
            var matcher = new Matcher();
            matcher.AddInclude(@"**\*.cshtml");

            var templates = matcher.GetResultsInFullPath(dir ?? _appEnv.ApplicationBasePath);

            return templates;
        }
    }
}
