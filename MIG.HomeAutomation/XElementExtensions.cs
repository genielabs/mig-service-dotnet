using System.Linq;
using System.Xml.Linq;

namespace MIG.Interfaces.HomeAutomation
{
    public static class XElementExtensions
    {
        public static XElement RemoveAllNamespaces(this XElement xmlDocument)
        {
            XElement xElement = new XElement(xmlDocument.Name.LocalName);
            foreach (XAttribute attribute in xmlDocument.Attributes().Where(x => !x.IsNamespaceDeclaration))
                xElement.Add(attribute);

            if (!xmlDocument.HasElements)
            {                
                xElement.Value = xmlDocument.Value;                
                return xElement;
            }

            xElement.Add(xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
            return xElement;
        }
    }
}
