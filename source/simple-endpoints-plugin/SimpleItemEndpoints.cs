using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Web;
using SobekCM.Core.Aggregations;
using SobekCM.Core.BriefItem;
using SobekCM.Core.Configuration.Engine;
using SobekCM.Core.FileSystems;
using SobekCM.Core.UI_Configuration.Citation;
using SobekCM.Core.Results;
using SobekCM.Engine_Library.ApplicationState;
using SobekCM.Tools;

namespace SobekCM.Engine_Library.Endpoints
{
    public class SimpleItemEndpoints : ItemServices
    {
        public void Simple_Item_XML(HttpResponse Response, List<string> UrlSegments, NameValueCollection QueryString, Microservice_Endpoint_Protocol_Enum Protocol, bool IsDebug)
        {
            // Must at least have one URL segment for the BibID
            if (UrlSegments.Count > 0)
            {
                Custom_Tracer tracer = new Custom_Tracer();

                try
                {
                    // Get the BibID and VID
                    string bibid = UrlSegments[0];
                    string vid = (UrlSegments.Count > 1) ? UrlSegments[1] : "00001";

                    tracer.Add_Trace("SimpleItemEndpoints.Simple_Item_XML", "Requested citation for " + bibid + ":" + vid);

                    // Get the brief item
                    tracer.Add_Trace("SimpleItemEndpoints.Simple_Item_XML", "Build full brief item");
                    Tuple<BriefItemInfo, Items.SobekCM_Item_Error> returnTuple = GetBriefItem(bibid, vid, null, tracer);

                    // Was the item null?
                    if ((returnTuple == null) || ( returnTuple.Item1 == null ))
                    {
                        // If this was debug mode, then just write the tracer
                        if (IsDebug)
                        {
                            tracer.Add_Trace("SimpleItemEndpoints.Simple_Item_XML", "NULL value returned from getBriefItem method");
                            Response.ContentType = "text/plain";
                            Response.Output.WriteLine("DEBUG MODE DETECTED");
                            Response.Output.WriteLine();

                            // Was an error received though?
                            if (( returnTuple != null ) && ( returnTuple.Item2 != null ))
                            {
                                Items.SobekCM_Item_Error itemError = returnTuple.Item2;
                                switch( itemError.Type)
                                {
                                    case Items.SobekCM_Item_Error_Type_Enum.Invalid_BibID:
                                        Response.Output.WriteLine("ERROR: Invalid BibID requested");
                                        break;

                                    case Items.SobekCM_Item_Error_Type_Enum.Invalid_VID:
                                        Response.Output.WriteLine("ERROR: Invalid VID requested");
                                        Response.Output.WriteLine("First valid VID is " + itemError.FirstValidVid);
                                        break;

                                    case Items.SobekCM_Item_Error_Type_Enum.System_Error:
                                        Response.Output.WriteLine("System ERROR detected while attempting to create the item");
                                        Response.Output.WriteLine(itemError.Message);
                                        break;
                                }

                                Response.Output.WriteLine();
                            }
                            
                            Response.Output.WriteLine(tracer.Text_Trace);
                        }
                        return;
                    }

                    // Get the brief item info
                    BriefItemInfo returnValue = returnTuple.Item1;

                    // If this was debug mode, then just write the tracer
                    if (IsDebug)
                    {
                        Response.ContentType = "text/plain";
                        Response.Output.WriteLine("DEBUG MODE DETECTED");
                        Response.Output.WriteLine();
                        Response.Output.WriteLine(tracer.Text_Trace);

                        return;
                    }

                    // If this is dark, do nothing
                    if (returnValue.Behaviors.Dark_Flag)
                    {
                        Response.ContentType = "text/plain";
                        Response.Output.WriteLine("Item is restricted");
                        Response.StatusCode = 403;
                        return;
                    }


                    // Create and write the basic item info
                    Response.Output.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");
                    Response.Output.WriteLine("<item bibid=\"" + returnValue.BibID + "\" vid=\"" + returnValue.VID + "\">");
                    Response.Output.WriteLine("    <title>" + HttpUtility.HtmlEncode(returnValue.Title) + "</title>");
                    Response.Output.WriteLine("    <url_item>" + Engine_ApplicationCache_Gateway.Settings.Servers.Application_Server_URL + returnValue.BibID + "/" + returnValue.VID + "/</url_item>");

                    // Add the thumbnail URL
                    if (!String.IsNullOrEmpty(returnValue.Behaviors.Main_Thumbnail))
                    {
                        try
                        {
                            Response.Output.WriteLine("    <url_thumbnail>" + Engine_ApplicationCache_Gateway.Settings.Servers.Image_URL +
                                                      SobekFileSystem.AssociFilePath(returnValue.BibID, returnValue.VID).Replace("\\", "/") + returnValue.Behaviors.Main_Thumbnail.Trim() + "</url_thumbnail>");
                        }
                        catch (Exception ee)
                        {
                            Response.Output.WriteLine("ERROR WRITING THUMBNAIL");
                            Response.Output.WriteLine(ee.Message);
                            Response.Output.WriteLine(ee.StackTrace);
                        }
                    }

                    Response.Output.WriteLine("    <metadata>");

                    // Step through the citation configuration here
                    CitationSet citationSet = Engine_ApplicationCache_Gateway.Configuration.UI.CitationViewer.Get_CitationSet();
                    foreach (CitationFieldSet fieldsSet in citationSet.FieldSets)
                    {
                        // Step through all the fields in this field set and write them
                        foreach (CitationElement thisField in fieldsSet.Elements)
                        {
                            // Look for a match in the item description
                            BriefItem_DescriptiveTerm briefTerm = returnValue.Get_Description(thisField.MetadataTerm);

                            // If no match, just continue
                            if ((briefTerm == null) || (briefTerm.Values.Count == 0))
                                continue;

                            // Get the clean XML tag for this term
                            string cleaned_metadata_term = HttpUtility.HtmlEncode(thisField.DisplayTerm.Replace(" ", "_").Replace("/", "_").Replace("__", "_").Replace("__", "_"));

                            foreach (BriefItem_DescTermValue term in briefTerm.Values)
                            {
                                Response.Output.WriteLine("        <" + cleaned_metadata_term + ">" + HttpUtility.HtmlEncode(term.Value) + "</" + cleaned_metadata_term + ">");
                            }
                        }
                    }

                    Response.Output.WriteLine("    </metadata>");
                    Response.Output.WriteLine("</item>");
                }
                catch (Exception ee)
                {
                    if (IsDebug)
                    {
                        Response.ContentType = "text/plain";
                        Response.Output.WriteLine("EXCEPTION CAUGHT!");
                        Response.Output.WriteLine();
                        Response.Output.WriteLine(ee.Message);
                        Response.Output.WriteLine();
                        Response.Output.WriteLine(ee.StackTrace);
                        Response.Output.WriteLine();
                        Response.Output.WriteLine(tracer.Text_Trace);
                        return;
                    }

                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Error completing request");
                    Response.StatusCode = 500;
                }
            }

   

        }

        public void Simple_Item_JSON(HttpResponse Response, List<string> UrlSegments, NameValueCollection QueryString, Microservice_Endpoint_Protocol_Enum Protocol, bool IsDebug)
        {

        }
    }
}
