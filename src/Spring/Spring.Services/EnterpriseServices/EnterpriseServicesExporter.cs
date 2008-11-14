#region License

/*
 * Copyright 2002-2008 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

#if (!NET_1_0 && !MONO)

#region Imports

using System;
using System.Collections;
using System.Diagnostics;
using System.EnterpriseServices;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using Spring.Objects.Factory;
using Spring.Util;

#endregion

namespace Spring.EnterpriseServices
{
    /// <summary>
    /// Exports specified components as ServicedComponents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class will create ServicedComponent wrapper for each of the
    /// specified components and register them with the Component Services.
    /// </para>
    /// <para>
    /// First you need to generate and register your components. This is done by writing a simple e.g. console application using a configuration as shown below:
    /// <code>
    /// &lt;!-- actual objects 'calculatorService' and 'simpleCalculatorService' are defined elsewhere --&gt;
    /// 
    /// &lt;!-- Define the component for exporting 'calculatorService' --&gt;
    /// &lt;object id=&quot;calculatorComponent&quot; type=&quot;Spring.EnterpriseServices.ServicedComponentExporter, 
    /// Spring.Services&quot;&gt;
    ///   &lt;property name=&quot;TargetName&quot; value=&quot;calculatorService&quot; /&gt;
    ///   &lt;property name=&quot;TypeAttributes&quot;&gt;
    ///     &lt;list&gt;
    ///       &lt;object type=&quot;System.EnterpriseServices.TransactionAttribute, System.EnterpriseServices&quot; /&gt;
    ///     &lt;/list&gt;
    ///   &lt;/property&gt;
    ///   &lt;property name=&quot;MemberAttributes&quot;&gt;
    ///     &lt;dictionary&gt;
    ///       &lt;entry key=&quot;*&quot;&gt;
    ///         &lt;list&gt;
    ///           &lt;object type=&quot;System.EnterpriseServices.AutoCompleteAttribute, System.EnterpriseServices&quot; /&gt;
    ///         &lt;/list&gt;
    ///       &lt;/entry&gt;
    ///     &lt;/dictionary&gt;
    ///   &lt;/property&gt;
    /// &lt;/object&gt;
    /// 
    /// &lt;!-- Define the component for exporting 'simpleCalculatorService' --&gt;
    /// &lt;object id=&quot;simpleCalculatorComponent&quot; type=&quot;Spring.EnterpriseServices.ServicedComponentExporter, 
    /// Spring.Services&quot;&gt;
    ///   &lt;property name=&quot;TargetName&quot; value=&quot;simpleCalculatorService&quot; /&gt;
    /// &lt;/object&gt;
    /// 
    /// &lt;!-- Export components into assembly and autoregister with COM+ --&gt;
    /// &lt;object type=&quot;Spring.EnterpriseServices.EnterpriseServicesExporter, Spring.Services&quot;&gt;    
    ///   &lt;!-- assembly name to generated - will generate 'Spring.Calculator.EnterpriseServices.dll'  --&gt;
    ///   &lt;property name=&quot;Assembly&quot; value=&quot;Spring.Calculator.EnterpriseServices&quot; /&gt;
    ///   
    ///   &lt;!-- 
    ///   use Spring's ContextRegistry for managing services. If true, requires a file  
    ///   'Spring.Calculator.EnterpriseServices.dll.spring-context.xml'  containing a 
    ///   &lt;spring/context /&gt; section placed next to the generated assembly.
    ///   --&gt;
    ///   &lt;property name=&quot;UseSpring&quot; value=&quot;true&quot; /&gt;
    ///   
    ///   &lt;property name=&quot;ApplicationName&quot; value=&quot;Spring Calculator Application&quot; /&gt;
    ///   &lt;property name=&quot;ActivationMode&quot; value=&quot;Library&quot; /&gt;
    ///   &lt;property name=&quot;Description&quot; value=&quot;Spring Calculator application&quot; /&gt;
    ///   &lt;property name=&quot;Components&quot;&gt;
    ///     &lt;list&gt;
    ///       &lt;ref object=&quot;calculatorComponent&quot; /&gt;
    ///       &lt;ref object=&quot;simpleCalculatorComponent&quot; /&gt;
    ///     &lt;/list&gt;
    ///   &lt;/property&gt;
    /// &lt;/object&gt;
    /// </code>
    /// </para>
    /// <para>
    /// To load your objectdefinitions at runtime of the components, place a configuration file next to the assembly 
    /// generated by the exporter, using the filename of the exported assembly, postfixing it with '.spring-context.config'. 
    /// Taking the example above, the file must be named 'Spring.Calculator.EnterpriseServices.dll.spring-context.xml' and look like:
    /// <code>
    /// &lt;--  --&gt;
    /// &lt;spring&gt;
    ///   &lt;context&gt;
    ///     &lt;resource uri=&quot;Config/services.xml&quot; /&gt;
    ///   &lt;/context&gt;
    /// &lt;/spring&gt;
    /// </code>
    /// This file should point to the service object definitions you exported using <see cref="EnterpriseServicesExporter"/> with a
    /// configuration as shown above.
    /// </para>
    /// </remarks>
    /// <seealso cref="ServicedComponentExporter"/>
    /// <author>Aleksandar Seovic</author>
    /// <author>Erich Eichinger</author>
    public class EnterpriseServicesExporter : IInitializingObject, IObjectFactoryAware
    {
        #region Fields

        private IObjectFactory objectFactory;

        private IList components = new ArrayList();
        private string applicationName;
        private string applicationId;
        private ActivationOption activationMode = ActivationOption.Library;
        private string description;
        private ApplicationAccessControlAttribute accessControl;
        private ApplicationQueuingAttribute applicationQueuing;
        private IList roles;
        private string assemblyName;
        private bool useSpring;

        #endregion

        #region Constructor(s) / Destructor

        /// <summary>
        /// Creates new enterprise services exporter.
        /// </summary>
        public EnterpriseServicesExporter()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets list of components to export.
        /// </summary>
        public IList Components
        {
            get { return components; }
            set { components = value; }
        }

        /// <summary>
        /// Gets or sets COM+ application name.
        /// </summary>
        public string ApplicationName
        {
            get { return applicationName; }
            set { applicationName = value; }
        }

        /// <summary>
        /// Gets or sets application identifier (GUID). Defaults to generated GUID if not specified.
        /// </summary>
        public string ApplicationId
        {
            get { return applicationId; }
            set { applicationId = value; }
        }

        /// <summary>
        /// Gets or sets application activation mode, which can be either <b>Server</b> or <b>Library</b> (default).
        /// </summary>
        public ActivationOption ActivationMode
        {
            get { return activationMode; }
            set { activationMode = value; }
        }

        /// <summary>
        /// Gets or sets application description.
        /// </summary>
        public string Description
        {
            get { return description; }
            set { description = value; }
        }

        /// <summary>
        /// Gets or sets access control attribute.
        /// </summary>
        public ApplicationAccessControlAttribute AccessControl
        {
            get { return accessControl; }
            set { accessControl = value; }
        }

        /// <summary>
        /// Gets or sets application queuing attribute.
        /// </summary>
        public ApplicationQueuingAttribute ApplicationQueuing
        {
            get { return applicationQueuing; }
            set { applicationQueuing = value; }
        }

        /// <summary>
        /// Gets or sets application roles.
        /// </summary>
        public IList Roles
        {
            get { return roles; }
            set { roles = value; }
        }

        /// <summary>
        /// Gets or sets name of the generated assembly that will contain serviced components.
        /// </summary>
        public string Assembly
        {
            get { return assemblyName; }
            set { assemblyName = value; }
        }

        ///<summary>
        /// Use Spring context to configure the serviced components.
        ///</summary>
        public bool UseSpring
        {
            get { return useSpring; }
            set { useSpring = value; }
        }

        #endregion

        #region IInitializingObject Members

        /// <summary>
        /// Called by Spring container after object is configured in order to initialize it.
        /// </summary>
        public void AfterPropertiesSet()
        {
            if (roles != null && roles.Count > 0)
            {
                RefreshRoles();
            }
            Export();
        }

        #endregion

        #region IObjectFactoryAware Members

        /// <summary>
        /// Sets object factory instance.
        /// </summary>
        public IObjectFactory ObjectFactory
        {
            set { objectFactory = value; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates ServicedComponent wrappers for the specified components and registers
        /// them with COM+ Component Services.
        /// </summary>
        public virtual void Export()
        {
            AssemblyName an = new AssemblyName();
            an.Name = assemblyName;
            an.Version = new Version("1.0.0.0");
            an.KeyPair = new StrongNameKeyPair(GetKeyPair());
            AssemblyBuilder proxyAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
            ModuleBuilder module = proxyAssembly.DefineDynamicModule(assemblyName, assemblyName + ".dll", true);
            ApplyAssemblyAttributes(proxyAssembly);

            Type baseType = typeof(ServicedComponent);
            if (UseSpring)
            {
                baseType = CreateSpringServicedComponentType(module);
            }

            foreach (ServicedComponentExporter definition in components)
            {
                definition.CreateWrapperType(module, baseType, objectFactory, UseSpring);
            }

            proxyAssembly.Save(assemblyName + ".dll");

            RegistrationConfig config = new RegistrationConfig();
            config.Application = applicationName;
            config.AssemblyFile = AppDomain.CurrentDomain.DynamicDirectory + assemblyName + ".dll";
            config.InstallationFlags = InstallationFlags.ReportWarningsToConsole | InstallationFlags.FindOrCreateTargetApplication | InstallationFlags.ReconfigureExistingApplication;

            RegistrationHelper regHelper = new RegistrationHelper();
            regHelper.InstallAssemblyFromConfig(ref config);
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Reads key pair from embedded resource.
        /// </summary>
        /// <returns>Key pair as a byte array.</returns>
        private byte[] GetKeyPair()
        {
            using (Stream keys = GetType().Assembly.GetManifestResourceStream("Spring.EnterpriseServices.EnterpriseServices.keys"))
            {
                byte[] bytes = new byte[keys.Length];
                keys.Read(bytes, 0, (int)keys.Length);
                return bytes;
            }
        }

        /// <summary>
        /// Applies custom attributes to generated assembly.
        /// </summary>
        /// <param name="assembly">Dynamic assembly to apply attributes to.</param>
        private void ApplyAssemblyAttributes(AssemblyBuilder assembly)
        {
            assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(typeof(ApplicationNameAttribute), applicationName));
            assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(typeof(ApplicationActivationAttribute), activationMode));
            if (applicationId != null)
            {
                assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(typeof(ApplicationIDAttribute), applicationId));
            }
            if (description != null)
            {
                assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(typeof(DescriptionAttribute), description));
                assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(typeof(AssemblyDescriptionAttribute), description));
            }
            if (accessControl != null)
            {
                assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(accessControl));
            }
            if (applicationQueuing != null)
            {
                assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(applicationQueuing));
            }
            if (roles != null)
            {
                foreach (SecurityRoleAttribute role in roles)
                {
                    assembly.SetCustomAttribute(ReflectionUtils.CreateCustomAttribute(typeof(SecurityRoleAttribute),
                                                                                      new object[] { role.Role }, role));
                }
            }
        }

        /// <summary>
        /// Replaces roles expressed using string with appropriate SecurityRoleAttribute instance.
        /// </summary>
        private void RefreshRoles()
        {
            for (int i = 0; i < roles.Count; i++)
            {
                object role = roles[i];
                if (role is string)
                {
                    roles[i] = ParseRole((string)role);
                }
            }
        }

        /// <summary>
        /// Parses string representation of SecurityRoleAttribute.
        /// </summary>
        /// <param name="roleString">Role definition string.</param>
        /// <returns>Configured SecurityRoleAttribute instance.</returns>
        private SecurityRoleAttribute ParseRole(string roleString)
        {
            string[] parts = roleString.Split(':');
            SecurityRoleAttribute role = new SecurityRoleAttribute(parts[0].Trim());
            if (parts.Length > 1)
            {
                role.Description = parts[1].Trim();
            }
            if (parts.Length > 2)
            {
                role.SetEveryoneAccess = bool.Parse(parts[2].Trim());
            }

            return role;
        }

        #endregion

        #region SpringServicedComponent generation

        /// <summary>
        /// Creates the SpringServicedComponent base class to derive all <see cref="ServicedComponent"/>s from.
        /// </summary>
        /// <example>
        /// <code>
        /// internal class SpringServicedComponent
        /// {
        ///    protected delegate object GetObjectHandler(ServicedComponent servicedComponent, string targetName);
        /// 
        ///    protected static readonly GetObjectHandler getObjectRef;
        ///         
        ///    static SpringServicedComponent()
        ///    {
        ///      // first look for a local copy
        ///      System.Reflection.Assembly servicesAssembly;
        ///      string servicesAssemblyPath = Path.Combine(
        ///                                 new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName
        ///                                 , &quot;Spring.Services.dll&quot; );
        ///      servicesAssembly = System.Reflection.Assembly.LoadFrom(servicesAssemblyPath);
        ///      if (servicesAssembly == null)
        ///      {
        ///        // then let the normal loader handle the typeload
        ///        servicesAssembly = System.Reflection.Assembly.Load(&quot;Spring.Services, culture=neutral, version=x.x.x.x, publicKey=xxxxxxxx&quot;);
        ///      }
        /// 
        ///      Type componentHelperType = servicesAssembly.GetType(&quot;Spring.EnterpriseServices.ServicedComponentHelper&quot;);
        ///      getObjectRef = (GetObjectHandler) Delegate.CreateDelegate(typeof(GetObjectHandler)
        ///                                                                         , componentHelperType.GetMethod(&quot;GetObject&quot;));
        ///    }        
        /// }
        /// </code>
        /// </example>
        private Type CreateSpringServicedComponentType(ModuleBuilder module)
        {
            Type delegateType = DefineDelegate(module);

            TypeBuilder typeBuilder = module.DefineType("SpringServicedComponent", System.Reflection.TypeAttributes.Public, typeof(ServicedComponent));
            ILGenerator il = null;
            FieldBuilder getObjectRef = typeBuilder.DefineField("getObject", delegateType, FieldAttributes.Family | FieldAttributes.Static);

            ConstructorBuilder typeCtor = typeBuilder.DefineTypeInitializer();
            il = typeCtor.GetILGenerator();
            Label methodEnd = il.DefineLabel();
            Label loadType = il.DefineLabel();
            Label tryBegin = il.BeginExceptionBlock();
            LocalBuilder fldAssembly = il.DeclareLocal(typeof(Assembly));
            LocalBuilder fldType = il.DeclareLocal(typeof(Type));

            il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("GetExecutingAssembly"));
            il.Emit(OpCodes.Callvirt, typeof(Assembly).GetProperty("Location").GetGetMethod());
            il.Emit(OpCodes.Newobj, typeof(FileInfo).GetConstructor(new Type[] {typeof(string)}));
            il.Emit(OpCodes.Call, typeof(FileInfo).GetProperty("DirectoryName").GetGetMethod());
            il.Emit(OpCodes.Ldstr, new FileInfo(typeof(ServicedComponentHelper).Assembly.Location).Name);

            il.Emit(OpCodes.Call, typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) }));
            il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new Type[] { typeof(string)}));
            il.Emit(OpCodes.Stloc, fldAssembly);
            il.Emit(OpCodes.Ldloc, fldAssembly);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brtrue_S, loadType);

            il.Emit(OpCodes.Ldstr, typeof(ServicedComponentHelper).Assembly.FullName);
            il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("Load", new Type[] { typeof(string) }));
            il.Emit(OpCodes.Stloc, fldAssembly);

            il.MarkLabel(loadType);
            il.Emit(OpCodes.Ldloc, fldAssembly);
            il.Emit(OpCodes.Ldstr, typeof(ServicedComponentHelper).FullName);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Call, typeof(Assembly).GetMethod("GetType", new Type[] { typeof(string), typeof(bool) }));
            il.Emit(OpCodes.Stloc, fldType);

            il.Emit(OpCodes.Ldtoken, delegateType);
            il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            il.Emit(OpCodes.Ldloc, fldType);            
            il.Emit(OpCodes.Ldstr, "GetObject");
            il.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("GetMethod", new Type[] { typeof(string) }));
            il.Emit(OpCodes.Call, typeof(Delegate).GetMethod("CreateDelegate", new Type[] { typeof(Type), typeof(MethodInfo) }));
            il.Emit(OpCodes.Castclass, delegateType);
            il.Emit(OpCodes.Stsfld, getObjectRef);
            il.Emit(OpCodes.Leave_S, methodEnd);

            il.BeginCatchBlock(typeof(Exception));
            il.Emit(OpCodes.Call, typeof(Trace).GetMethod("WriteLine", new Type[] { typeof(object) }));
            il.EndExceptionBlock();
            il.MarkLabel(methodEnd);
            il.Emit(OpCodes.Ret);
            return typeBuilder.CreateType();
        }

        private static readonly MethodAttributes ConstructorAttributes = MethodAttributes.Public |
            MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;


        private Type DefineDelegate(ModuleBuilder module)
        {
            MethodAttributes methodAtts = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;

            TypeBuilder typeBuilder = module.DefineType("GetObjectHandler", TypeAttributes.Public | TypeAttributes.Sealed, typeof(MulticastDelegate));
            ConstructorBuilder cb = typeBuilder.DefineConstructor(ConstructorAttributes, CallingConventions.Standard, new Type[] { typeof(object), typeof(IntPtr) });
            cb.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.Runtime);

            MethodBuilder mb1 = typeBuilder.DefineMethod("BeginInvoke", methodAtts, CallingConventions.Standard, typeof(IAsyncResult)
                                , new Type[] { typeof(ServicedComponent), typeof(string), typeof(AsyncCallback), typeof(object) });
            mb1.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.Runtime);
            MethodBuilder mb2 = typeBuilder.DefineMethod("EndInvoke", methodAtts, CallingConventions.Standard, typeof(object)
                                , new Type[] { typeof(IAsyncResult) });
            mb2.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.Runtime);
            MethodBuilder mb3 = typeBuilder.DefineMethod("Invoke", methodAtts, CallingConventions.Standard, typeof(object)
                                , new Type[] { typeof(ServicedComponent), typeof(string) });
            mb3.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.Runtime);
            return typeBuilder.CreateType();
        }

        #endregion
    }
}

#endif // (!NET_1_0)
