using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using Autoklicker;

// WPF queues OnStartup even when a test pumps the dispatcher without calling Run.
// Keep the production single-instance startup out of isolated window/service tests.
internal sealed class TestApp : App
{
    protected override void OnStartup(StartupEventArgs e) { }

    internal void InitializeTheme()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestTheme.xaml"));
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }
}
