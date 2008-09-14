using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;

using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    class AddInManager
    {
        private List<IAddIn> _addIns = new List<IAddIn>();
        
        public void Load(Server server, Session session)
        {
            LoadAddInFromAssembly(Assembly.GetExecutingAssembly());
            
            String addinsBase = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "AddIns");
            if (Directory.Exists(addinsBase))
            {
                foreach (String fileName in Directory.GetFiles(addinsBase, "*.dll"))
                {
                    try
                    {
                        Assembly asm = Assembly.LoadFile(fileName);
                        LoadAddInFromAssembly(asm);
                    }
                    catch (Exception e)
                    {
                    }
                }
            }

            foreach (IAddIn addIn in _addIns)
                addIn.Initialize(server, session);
        }
    
        private void LoadAddInFromAssembly(Assembly asm)
        {
            Type addinType = typeof(IAddIn);
            foreach (Type t in asm.GetTypes())
            {
                if (addinType.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    Trace.WriteLine(String.Format("Load AddIn: {0}", t));
                    IAddIn addIn = Activator.CreateInstance(t) as IAddIn;

                    _addIns.Add(addIn);
                }
            }
        }
    }
}
