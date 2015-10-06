using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Text;

namespace cproject_sort
{
    class Program
    {
        enum SortType { None, Ascending, Decending };
        enum makeBackup { No, Yes };
        static string inputFile;
        static string outputFile;
        static string attribute;
        static makeBackup backup;

        /// <summary>
        /// Parse arguments and puts to global variables
        /// </summary>
        /// <param name="args"></param>
        static void decompArgs(string[] args)
        {
            foreach (string argument in args)
            {
                if (argument.Length >= 2)
                {
                    switch (argument[1])
                    {
                        case 'f':
                            inputFile = argument.Substring(2, argument.Length - 2);
                            outputFile = inputFile;
                            break;
                        case 'o':
                            outputFile = argument.Substring(2, argument.Length - 2);
                            break;
                        case 's':
                            attribute = argument.Substring(2, argument.Length - 2);
                            break;
                        case 'b':
                            backup = makeBackup.No;
                            break;
                        case '?':
                            Console.Out.WriteLine("Parameters:");
                            Console.Out.WriteLine(" -f<file> - input filename, default is .cproject");
                            Console.Out.WriteLine(" -o<file> - output filename, default is .cproject");
                            Console.Out.WriteLine(" -s<attrib>,<attrib> - attribute for sort, default is id,configurationName");
                            Console.Out.WriteLine(" -b - without make backup file");
                            Console.Out.WriteLine(" -? - this help\n");
                            Environment.Exit(0);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Make archive file
        /// </summary>
        /// <param name="inputFile"></param>
        static void MakeArchive(string inputFile)
        {
            if (System.IO.File.Exists(inputFile + ".bak"))
                System.IO.File.Delete(inputFile + ".bak");

            System.IO.File.Copy(inputFile, inputFile + ".bak");
        }

        /// <summary>
        /// Sort XML Document, add special strings to Head, etc.
        /// </summary>
        /// <param name="sourceDoc"></param>
        /// <param name="level"></param>
        /// <param name="attribute"></param>
        /// <param name="sortAttributes"></param>
        /// <returns></returns>
        static XDocument RunSort(string sourceDoc, string targetDoc, int level, string attribute, SortType sortAttributes)
        {
            XDocument doc = XDocument.Load(sourceDoc);
            string[] tokens = attribute.Split(new[] { "," }, StringSplitOptions.None);
            XDeclaration declaration = null;
            XNode firstNode = null;

            // save firstNode and declaration because this will be removed
            try
            {
                firstNode = doc.FirstNode;
                declaration = doc.Declaration;
            }
            catch (IOException) { }

            // sort for each Attributes
            foreach (string token in tokens)
            {
                if (token.Length > 0)
                {
                    XDocument sortedDoc = Sort(doc, level, token, sortAttributes);
                    doc = sortedDoc;
                }
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = System.Text.Encoding.ASCII;
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;

            doc.Declaration = declaration;      // nefunguje, prebije to settings.Encoding
            if ((firstNode != null)
            && (firstNode.ToString().Contains("fileVersion")))
                doc.AddFirst(firstNode);

            using (XmlWriter writer = XmlWriter.Create(sourceDoc + ".tmp", settings))
            {
                doc.WriteTo(writer);
                writer.Flush();
            }

            AddHead(sourceDoc + ".tmp", targetDoc, (declaration == null) ? "" : declaration.ToString() + "\n");
            System.IO.File.Delete(sourceDoc + ".tmp");
            return doc;
        }

        /// <summary>
        /// Add special string to Head of file (.cproject specials)
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputFile"></param>
        static void AddHead(string inputFile, string outputFile, string declaration)
        {
            byte[] buffer = new byte[1024];
            Stream writer_txt = File.Create(outputFile);

            // "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n";
            for (int i = 0; i < declaration.Length; i++) writer_txt.WriteByte((byte)declaration[i]);

            using (Stream input = File.OpenRead(inputFile))
            {
                long bytesRead;
                while (input.Position < input.Length)
                {
                    long remaining = input.Length - input.Position;
                    while (remaining > 0
                       && (bytesRead = input.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length))) > 0)
                    {
                        writer_txt.Write(buffer, 0, (int)bytesRead);
                        remaining -= bytesRead;

                    }
                }
            }
        }

        /// <summary>
        /// Run sort module
        /// </summary>
        /// <param name="file"></param>
        /// <param name="level"></param>
        /// <param name="attribute"></param>
        /// <param name="sortAttributes"></param>
        /// <returns></returns>
        static XDocument Sort(XDocument file, int level, string attribute, SortType sortAttributes)
        {
            return new XDocument(SortElement(file.Root, level, attribute, sortAttributes));
        }

        /// <summary>
        /// Sort module
        /// </summary>
        /// <param name="element"></param>
        /// <param name="level"></param>
        /// <param name="attribute"></param>
        /// <param name="sortAttributes"></param>
        /// <returns></returns>
        static XElement SortElement(XElement element, int level, string attribute, SortType sortAttributes)
        {
            XElement newElement = new XElement(element.Name,
                from child in element.Elements()
                orderby
                    (child.Ancestors().Count() > level)
                        ? (
                            (child.HasAttributes && !string.IsNullOrEmpty(attribute) && child.Attribute(attribute) != null)
                                ? child.Attribute(attribute).Value.ToString()
                                : child.Name.ToString()
                            )
                        : ""  //End of the orderby clause
                select SortElement(child, level, attribute, sortAttributes));
            if (element.HasAttributes)
            {
                switch (sortAttributes)
                {
                    case SortType.None:
                        foreach (XAttribute attrib in element.Attributes())
                        {
                            newElement.SetAttributeValue(attrib.Name, attrib.Value);
                        }
                        break;
                    case SortType.Ascending:
                        foreach (XAttribute attrib in element.Attributes().OrderBy(a => a.Name.ToString()))
                        {
                            newElement.SetAttributeValue(attrib.Name, attrib.Value);
                        }
                        break;
                    case SortType.Decending:
                        foreach (XAttribute attrib in element.Attributes().OrderByDescending(a => a.Name.ToString()))
                        {
                            newElement.SetAttributeValue(attrib.Name, attrib.Value);
                        }
                        break;
                    default:
                        break;
                }
            }
            return newElement;
        }

        /// <summary>
        /// Main
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.Out.WriteLine("Sort XML according attributes/elements.  Software by Zdeno Sekerak (c) 2015.");

            // default
            inputFile = ".cproject";
            outputFile = ".cproject";
            attribute = "id,configurationName";
            backup = makeBackup.Yes;
            decompArgs(args);

            // lite test
            if (!System.IO.File.Exists(inputFile))
            {
                Console.Error.WriteLine("Run program with -? for help");
                Console.Error.WriteLine("Error: Desired file " + inputFile + " doesn't exist.");
                return;
            }

            if (backup == makeBackup.Yes)
                MakeArchive(inputFile);

            RunSort(inputFile, outputFile, 0, attribute, SortType.Ascending);
            Console.Out.WriteLine("Sort " + inputFile + " file is completed.");
        }
    }
}
