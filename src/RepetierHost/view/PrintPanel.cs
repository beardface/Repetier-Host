/*
   Copyright 2011 repetier repetierdev@gmail.com

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RepetierHost.model;
using RepetierHost.view.utils;

namespace RepetierHost.view
{
    public enum PrinterStatus {disconnected,idle,heatingExtruder,heatingBed,motorStopped,jobPaused,jobKilled,jobFinsihed }
    public partial class PrintPanel : UserControl
    {
        PrinterConnection con;
        GCodeAnalyzer ann;
        LinkedList<string> commands = new LinkedList<string>();
        int commandPos = 0;
        bool createCommands = true;
        float lastx = -1000, lasty = -1000, lastzl = -1000, lastzr = -1000;
        private PrinterStatus status = PrinterStatus.disconnected;
        private long statusSet=0;
        public PrintPanel()
        {
            InitializeComponent();
            con = Main.conn;
            ann = con.analyzer;
            con.eventConnectionChange += ConnectionChanged;
            ann.fanVoltage = trackFanVoltage.Value;
          //  ann.eventPosChanged += coordUpdate;
            ann.eventChange += analyzerChange;
            UpdateConStatus(false);

            float volt = 100f * trackFanVoltage.Value / 255;
            labelVoltage.Text = Trans.T1("L_OUTPUT_PERCENT", volt.ToString("0.0")); //"Output " + volt.ToString("0.0") + "%";
            labelLaserVoltage.Text = Trans.T1("L_OUTPUT_PERCENT", volt.ToString("0.0")); //"Output " + volt.ToString("0.0") + "%";
            arrowButtonXMinus.PossibleValues = Custom.GetString("xyMoveDistances", arrowButtonXMinus.PossibleValues);
            arrowButtonXPlus.PossibleValues = Custom.GetString("xyMoveDistances", arrowButtonXPlus.PossibleValues);
            arrowButtonYMinus.PossibleValues = Custom.GetString("xyMoveDistances", arrowButtonYMinus.PossibleValues);
            arrowButtonYPlus.PossibleValues = Custom.GetString("xyMoveDistances", arrowButtonYPlus.PossibleValues);
            if (Custom.GetBool("noPowerControlButton", false))
                switchPower.Visible = false;
            timer.Start();

            if (Main.main != null)
            {
                translate();
                Main.main.languageChanged += translate;
            }
        }
        public void translate() {
            buttonSend.Text = Trans.T("B_SEND");
            buttonSimulateOK.Text = Trans.T("B_SIMULATE_OK");
            buttonStopMotor.Text = Trans.T("B_STOP_MOTOR");
            buttonGoDisposeArea.Text = Trans.T("B_PARK");
            //buttonRetract.Text = Trans.T("B_RETRACT");
            //buttonExtrude.Text = Trans.T("B_EXTRUDE");
            float volt = 100f * trackFanVoltage.Value / 255;
            labelVoltage.Text = Trans.T1("L_OUTPUT_PERCENT", volt.ToString("0.0")); //"Output " + volt.ToString("0.0") + "%";
            switchPower.TextOff = switchPower.TextOn = Trans.T("B_POWER");
            switchFanOn.TextOff = switchFanOn.TextOn = Trans.T("B_FAN");
            switchLaserOn.TextOff = switchFanOn.TextOn = Trans.T("B_LASER");
            groupBox_Fan.Text = Trans.T("L_FAN");
            groupBox_Fan.Text = Trans.T("L_LASER");
            switchErrors.TextOff = switchErrors.TextOn = Trans.T("B_DEBUG_ERRORS");
            switchEcho.TextOff = switchEcho.TextOn = Trans.T("B_DEBUG_ECHO");
            switchDryRun.TextOff = switchDryRun.TextOn = Trans.T("B_DRY_RUN");
            switchInfo.TextOff = switchInfo.TextOn = Trans.T("B_DEBUG_INFO");
            groupSpeedMultiply.Text = Trans.T("L_SPEED_MULTIPLY");
            //labelTemp.Text = labelTemp2.Text = Trans.T("L_TEMP");
            labelFeedrate.Text = Trans.T("L_FEEDRATE:");
            labelFlowrate.Text = Trans.T("L_FLOWRATE:");
            groupDebugOptions.Text = Trans.T("L_DEBUG_OPTIONS");
            Status = status;
        }
        public void updateStatus()
        {
            TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
            long timestamp = (long)t.TotalSeconds;
            long diff = timestamp - statusSet;
            
            if (Main.conn.connected == false)
            {
                if (status != PrinterStatus.disconnected)
                    Status = PrinterStatus.disconnected;
            }
            else if (Main.conn.paused && status != PrinterStatus.jobPaused)
                Status = PrinterStatus.jobPaused;
            else if (status == PrinterStatus.jobPaused && !Main.conn.paused)
                Status = PrinterStatus.idle;
            else if (status == PrinterStatus.idle && diff > 0)
                Status = PrinterStatus.idle;
            else if (status == PrinterStatus.motorStopped || status == PrinterStatus.jobKilled || status == PrinterStatus.jobFinsihed)
            {
                if (diff > 30) // remove message after 30 seconds
                    Status = PrinterStatus.idle;
            }
            else if (status == PrinterStatus.disconnected && Main.conn.connected)
                Status = PrinterStatus.idle;
        }
        public MethodInvoker SetStatusJobFinished = delegate {Main.main.printPanel.Status = PrinterStatus.jobFinsihed;};
        public MethodInvoker SetStatusJobKilled = delegate { Main.main.printPanel.Status = PrinterStatus.jobKilled; };
        public PrinterStatus Status
        {
            set
            {
                TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
                long timestamp = (long)t.TotalSeconds;
                statusSet = timestamp;
                status = value;
                switch (value)
                {
                    case PrinterStatus.disconnected:
                        labelStatus.Text = Trans.T("L_DISCONNECTED"); // "Disconnected";
                        break;
                    case PrinterStatus.heatingBed:
                        labelStatus.Text = Trans.T("L_HEATING_BED"); // "Heating bed";
                        break;
                    case PrinterStatus.heatingExtruder:
                        labelStatus.Text = Trans.T("L_HEATING_EXTRUDER"); // "Heating extruder";
                        break;
                    case PrinterStatus.jobKilled:
                        labelStatus.Text = Trans.T("L_PRINT_JOB_KILLED"); //"Print job killed";
                        break;
                    case PrinterStatus.jobPaused:
                        labelStatus.Text = Trans.T("L_PRINT_JOB_PAUSED"); // "Print job paused";
                        break;
                    case PrinterStatus.jobFinsihed:
                        labelStatus.Text = Trans.T("L_PRINT_JOB_FINISHED"); // "Print job finished";
                        break;
                    default:
                    case PrinterStatus.idle:
                        if (Main.conn.job.mode==1)
                        {
                            if (Main.conn.analyzer.uploading)
                                labelStatus.Text = Trans.T("L_UPLOADING..."); //"Uploading ...";
                            else
                                labelStatus.Text = Trans.T1("L_PRINTING_JOB_ETA",Main.conn.job.ETA); //Printing job ETA " + Main.conn.job.ETA;
                        }
                        else
                        {
                            if (Main.conn.injectCommands.Count == 0)
                                labelStatus.Text = Trans.T("L_IDLE"); //"Idle";
                            else
                                labelStatus.Text = Trans.T1("L_X_COMMANDS_WAITING", Main.conn.injectCommands.Count.ToString()); // +" commands waiting";
                        }
                        break;
                }
            }
        }
        public void ConnectionChanged(string msg) {
            UpdateConStatus(Main.conn.serial != null || Main.conn.isVirtualActive);
        }
       
        public void analyzerChange() {
            createCommands = false;
            switchFanOn.On = ann.fanOn;
            switchLaserOn.On = ann.laserOn;
            trackFanVoltage.Value = ann.fanVoltage;
            trackLaserVoltage.Value = ann.laserVoltage;
            switchPower.On = ann.powerOn;
            sliderSpeed.Value = con.speedMultiply;
            numericUpDownSpeed.Value = con.speedMultiply;
            sliderFlowrate.Value = con.flowMultiply;
            numericUpDownFlow.Value = con.flowMultiply;
            createCommands = true;
        }
        private void coordUpdate(GCode code,float x,float y,float zl,float zr) {
            if (x != -lastx || x==0)
            {
                labelX.Text = "X=" + x.ToString("0.00");
                if (ann.hasXHome)
                    labelX.ForeColor = SystemColors.ControlText;
                else
                    labelX.ForeColor = Color.Red;
                lastx = x;
            }
            if (y != lasty || y==0)
            {
                labelY.Text = "Y=" + y.ToString("0.00");
                if (ann.hasYHome)
                    labelY.ForeColor = SystemColors.ControlText;
                else
                    labelY.ForeColor = Color.Red;
                lasty = y;
            }
            if (zl != lastzl || zl==0)
            {
                labelZL.Text = "ZL=" + zl.ToString("0.00");
                if (ann.hasZHome)
                    labelZL.ForeColor = SystemColors.ControlText;
                else
                    labelZL.ForeColor = Color.Red;
                lastzl = zl;
            }
            if (zr != lastzr || zr == 0)
            {
                labelZR.Text = "ZR=" + zr.ToString("0.00");
                if (ann.hasZHome)
                    labelZR.ForeColor = SystemColors.ControlText;
                else
                    labelZR.ForeColor = Color.Red;
                lastzr = zr;
            }
        }
        public void UpdateConStatus(bool c)
        {
            Main.main.toolRunJob.Enabled = c;
            Main.main.toolStripSDCard.Enabled = c;
            Main.main.menuSDCardManager.Enabled = c;
           // switchConnect.On = c;
            textGCode.Enabled = c;
            switchFanOn.Enabled = c;
            trackFanVoltage.Enabled = c;
            switchLaserOn.Enabled = c;
            trackLaserVoltage.Enabled = c;
            buttonSend.Enabled = c;
            buttonHomeAll.Enabled = c;
            buttonHomeX.Enabled = c;
            buttonHomeY.Enabled = c;
            buttonHomeZ.Enabled = c;
            buttonStopMotor.Enabled = c;
            switchPower.Enabled = c;
            switchEcho.Enabled = c;
            switchInfo.Enabled = c;
            switchDryRun.Enabled = c;
            switchErrors.Enabled = c;
            buttonGoDisposeArea.Enabled = c;
            buttonSimulateOK.Enabled = c;
            //buttonJobStatus.Enabled = c;
            arrowButtonXMinus.Enabled = c;
            arrowButtonXPlus.Enabled = c;
            arrowButtonYMinus.Enabled = c;
            arrowButtonYPlus.Enabled = c;
            arrowButtonZLMinus.Enabled = c;
            arrowButtonZLPlus.Enabled = c;
            arrowButtonZMinus.Enabled = c;
            arrowButtonZPlus.Enabled = c;
            arrowButtonZRPlus.Enabled = c;
            arrowButtonZRMinus.Enabled = c;
            sliderSpeed.Enabled = c && (con.isMarlin || con.isRepetier);
            sliderFlowrate.Enabled = c && (con.isMarlin || con.isRepetier);
            numericUpDownSpeed.Enabled = c && (con.isMarlin || con.isRepetier);
            if (c) sendDebug();
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (textGCode.Text.Length < 2) return;
            con.injectManualCommand(textGCode.Text);
            commands.AddLast(textGCode.Text);
            if (commands.Count > 100)
                commands.RemoveFirst();
            commandPos = commands.Count;
            textGCode.Text = "";
        }

        private void sendDebug()
        {
            if (con.serial == null && !con.isVirtualActive) return;
            int v = 0;
            if (switchEcho.On) v += 1;
            if (switchInfo.On) v += 2;
            if (switchErrors.On) v += 4;
            if (switchDryRun.On) v += 8;
            con.GetInjectLock();
            con.injectManualCommand("M111 S" + v);
            con.ReturnInjectLock();
        }

        private void buttonHomeX_Click(object sender, EventArgs e)
        {
            con.GetInjectLock();
            con.injectManualCommand("G28 X0");
            con.ReturnInjectLock();
        }

        private void buttonHomeY_Click(object sender, EventArgs e)
        {
            con.GetInjectLock();
            con.injectManualCommand("G28 Y0");
            con.ReturnInjectLock();
        }

        private void buttonHomeZ_Click(object sender, EventArgs e)
        {
            con.GetInjectLock();
            con.injectManualCommand("G28 Z0");
            con.ReturnInjectLock();
        }

        private void buttonHomeAll_Click(object sender, EventArgs e)
        {
            con.GetInjectLock();
            con.injectManualCommand("G28 X0 Y0 Z0");
            con.ReturnInjectLock();
        }
        private void moveHead(string axis,float amount) {
            con.GetInjectLock();
            bool wasrel = con.analyzer.relative;
            //if(!wasrel) 
                con.injectManualCommand("G91");
                if (axis.Equals("Z"))
                    con.injectManualCommand("G1 " + axis + amount.ToString(GCode.format) + " F" + con.maxZFeedRate.ToString(GCode.format));
                else if (axis.Equals("L"))
                    con.injectManualCommand("G0 L" + amount.ToString(GCode.format) + " F" + con.maxZFeedRate.ToString(GCode.format));
                else if (axis.Equals("R"))
                    con.injectManualCommand("G0 R" + amount.ToString(GCode.format) + " F" + con.maxZFeedRate.ToString(GCode.format));
                else
                    con.injectManualCommand("G1 " + axis + amount.ToString(GCode.format) + " F" + con.travelFeedRate.ToString(GCode.format));
            //if (!wasrel) 
                con.injectManualCommand("G90");
            con.ReturnInjectLock();
        }

        private void buttonXM100_Click(object sender, EventArgs e)
        {
            moveHead("X", -100);
        }

        private void buttonXM10_Click(object sender, EventArgs e)
        {
            moveHead("X", -10);
        }

        private void buttonXM1_Click(object sender, EventArgs e)
        {
            moveHead("X", -1);
        }

        private void buttonXM01_Click(object sender, EventArgs e)
        {
            moveHead("X", -0.1f);
        }

        private void buttonXP01_Click(object sender, EventArgs e)
        {
            moveHead("X", 0.1f);
        }

        private void buttonXP1_Click(object sender, EventArgs e)
        {
            moveHead("X", 1);
        }

        private void buttonXP10_Click(object sender, EventArgs e)
        {
            moveHead("X", 10);
        }

        private void buttonXP100_Click(object sender, EventArgs e)
        {
            moveHead("X", 100);
        }

        private void buttonYM100_Click(object sender, EventArgs e)
        {
            moveHead("Y", -100);
        }

        private void buttonYM10_Click(object sender, EventArgs e)
        {
            moveHead("Y", -10);
        }

        private void buttonYM1_Click(object sender, EventArgs e)
        {
            moveHead("Y", -1);
        }

        private void buttonYM01_Click(object sender, EventArgs e)
        {
            moveHead("Y", -0.1f);
        }

        private void buttonYP01_Click(object sender, EventArgs e)
        {
            moveHead("Y", 0.1f);
        }

        private void buttonYP1_Click(object sender, EventArgs e)
        {
            moveHead("Y", 1);
        }

        private void buttonYP10_Click(object sender, EventArgs e)
        {
            moveHead("Y", 10);
        }

        private void buttonYP100_Click(object sender, EventArgs e)
        {
            moveHead("Y", 100);
        }

        private void buttonZM100_Click(object sender, EventArgs e)
        {
            moveHead("Z", -100);
        }

        private void buttonZM10_Click(object sender, EventArgs e)
        {
            moveHead("Z", -10);
        }

        private void buttonZM1_Click(object sender, EventArgs e)
        {
            moveHead("Z", -1);
        }

        private void buttonZM01_Click(object sender, EventArgs e)
        {
            moveHead("Z", -0.1f);
        }

        private void buttonZP01_Click(object sender, EventArgs e)
        {
            moveHead("Z", 0.1f);
        }

        private void buttonZP1_Click(object sender, EventArgs e)
        {
            moveHead("Z", 1);
        }

        private void buttonZP10_Click(object sender, EventArgs e)
        {
            moveHead("Z", 10);
        }

        private void buttonZP100_Click(object sender, EventArgs e)
        {
            moveHead("Z", 100);
        }

        private void switchFanOn_Change(SwitchButton b)
        {
            if (Main.conn.connected == false) return;
            if (!createCommands) return;
            con.GetInjectLock();
            if (switchFanOn.On)
            {
                //if(ann.fanVoltage!=trackFanVoltage.Value)
                    con.injectManualCommand("M106 S" + trackFanVoltage.Value);
            }
            else
            {
                con.injectManualCommand("M107");
            }
            con.ReturnInjectLock();
        }

        private void switchLaserOn_Change(SwitchButton b)
        {
            if (Main.conn.connected == false) return;
            if (!createCommands) return;
            con.GetInjectLock();
            if (switchLaserOn.On)
            {
                //if(ann.fanVoltage!=trackFanVoltage.Value)
                con.injectManualCommand("M600 S" + trackFanVoltage.Value);
            }
            else
            {
                con.injectManualCommand("M601");
            }
            con.ReturnInjectLock();
        }

        private void trackFanVoltage_ValueChanged(object sender, EventArgs e)
        {
            float volt = 100f*trackFanVoltage.Value/255;
            labelVoltage.Text = Trans.T1("L_OUTPUT_PERCENT", volt.ToString("0.0"));
            if (!createCommands) return;
            //switchFanOn.On = true;
            if(switchFanOn.On)
                switchFanOn_Change(null);
        }

        private void trackLaserVoltage_ValueChanged(object sender, EventArgs e)
        {
            float volt = 100f * trackLaserVoltage.Value / 255;
            labelLaserVoltage.Text = Trans.T1("L_OUTPUT_PERCENT", volt.ToString("0.0"));
            if (!createCommands) return;

            if (switchLaserOn.On)
                switchLaserOn_Change(null);
        }

        private void switchEcho_Change(SwitchButton b)
        {
            sendDebug();
        }

        private void switchInfo_Change(SwitchButton b)
        {
            sendDebug();
        }

        private void switchErrors_Change(SwitchButton b)
        {
            sendDebug();
        }

        private void switchDryRun_Change(SwitchButton b)
        {
            sendDebug();
        }

        private void switchPower_Change(SwitchButton b)
        {
            if (Main.conn.connected == false) return;
            con.GetInjectLock();
            if (switchPower.On)
            {
                con.injectManualCommand("M80");
            }
            else
            {
                con.injectManualCommand("M81");
            }
            con.ReturnInjectLock();
        }

        private void textGCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                buttonSend_Click(null, null);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                commandPos--;
                if (commandPos < 0) commandPos = 0;
                if (commandPos < commands.Count)
                    textGCode.Text = commands.ElementAt(commandPos);
                textGCode.SelectionLength = 0;
                textGCode.SelectionStart = textGCode.Text.Length;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                commandPos++;
                if (commandPos > commands.Count)
                    commandPos = commands.Count;
                if (commandPos < commands.Count)
                    textGCode.Text = commands.ElementAt(commandPos);
                textGCode.SelectionLength = 0;
                textGCode.SelectionStart = textGCode.Text.Length;
                e.Handled = true;
            }
        }
        private void float_Validating(object sender, CancelEventArgs e)
        {
            TextBox box = (TextBox)sender;
            try
            {
                float.Parse(box.Text);
                errorProvider.SetError(box, "");
            }
            catch
            {
                errorProvider.SetError(box, Trans.T("L_NOT_A_NUMBER")); //"Not a number.");
            }
        }
        private void floatPos_Validating(object sender, CancelEventArgs e)
        {
            TextBox box = (TextBox)sender;
            try
            {
                float x = float.Parse(box.Text);
                if (x >= 0)
                    errorProvider.SetError(box, "");
                else
                    errorProvider.SetError(box, Trans.T("L_POSITIVE_NUMBER_REQUIRED")); //"Positive number required.");
            }
            catch
            {
                errorProvider.SetError(box, Trans.T("L_NOT_A_NUMBER"));
            }
        }
        private void int_Validating(object sender, CancelEventArgs e)
        {
            TextBox box = (TextBox)sender;
            try
            {
                int.Parse(box.Text);
                errorProvider.SetError(box, "");
            }
            catch
            {
                errorProvider.SetError(box,Trans.T("L_NOT_AN_INTEGER"));// "Not an integer.");
            }
        }

        private void buttonGoDisposeArea_Click(object sender, EventArgs e)
        {
            con.doDispose();
        }

        private void buttonSimulateOK_Click(object sender, EventArgs e)
        {
            con.analyzeResponse("ok");
        }

        private void buttonJobStatus_Click(object sender, EventArgs e)
        {
            JobStatus.ShowStatus();
        }

        private void textGCode_KeyPress(object sender, KeyPressEventArgs e)
        {
            {
                if (e.KeyChar == '\r')
                    e.Handled = true;
            }
        }

        private void buttonStopMotor_Click(object sender, EventArgs e)
        {
            con.injectManualCommand("M84");
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            coordUpdate(null, ann.x, ann.y, ann.zl, ann.zr);
            updateStatus();
        }


        private void sliderSpeed_ValueChanged(object sender, EventArgs e)
        {
            if (!createCommands) return;
            //labelSpeed.Text = sliderSpeed.Value.ToString() + "%";
            int oldcon = con.speedMultiply;
            if (sender == sliderSpeed)
            {
                if (con.speedMultiply != sliderSpeed.Value)
                {
                    con.speedMultiply = sliderSpeed.Value;
                    numericUpDownSpeed.Value = con.speedMultiply;
                }
            }
            else
            {
                if (con.speedMultiply != numericUpDownSpeed.Value)
                {
                    con.speedMultiply = (int)numericUpDownSpeed.Value;
                    sliderSpeed.Value = con.speedMultiply;
                }
            }
            if (oldcon != con.speedMultiply && con.connected && (con.isMarlin || con.isRepetier))
            {
                con.ignoreFeedback();
                con.injectManualCommand("M220 S" + sliderSpeed.Value.ToString());
            }
        }

        private void XY_arrowValueChanged(ArrowButton sender, string value)
        {
            if (value.Length == 0)
                labelMoveDist.Text = "";
            else
                labelMoveDist.Text = value + " mm";
        }

        private void ZL_arrowValueChanged(ArrowButton sender, string value)
        {
            if (value.Length == 0)
                labelZLDiff.Text = "";
            else
                labelZLDiff.Text = value + " mm";
        }

        private void ZR_arrowValueChanged(ArrowButton sender, string value)
        {
            if (value.Length == 0)
                labelZLDiff.Text = "";
            else
                labelZLDiff.Text = value + " mm";
        }

        private void arrowButtonXPlus_Click(object sender, EventArgs e)
        {
            float d = ((ArrowButton)sender).CurrentValueF;
            if (ann.hasXHome && d + ann.x > Main.printerSettings.XMax) d = Main.printerSettings.XMax - ann.x;
            moveHead("X", d);
        }

        private void arrowButtonXMinus_Click(object sender, EventArgs e)
        {
            float d = -((ArrowButton)sender).CurrentValueF;
            if (ann.hasXHome && d + ann.x < Main.printerSettings.XMin) d = Main.printerSettings.XMin - ann.x;
            moveHead("X", d);
        }

        private void arrowButtonYPlus_Click(object sender, EventArgs e)
        {
            float d = ((ArrowButton)sender).CurrentValueF;
            if (ann.hasYHome && d + ann.y > Main.printerSettings.YMax) d = Main.printerSettings.YMax - ann.y;
            moveHead("Y",d);

        }

        private void arrowButtonYMinus_Click(object sender, EventArgs e)
        {
            float d = -((ArrowButton)sender).CurrentValueF;
            if (ann.hasYHome && d + ann.y < Main.printerSettings.YMin) d = Main.printerSettings.YMin - ann.y;
            moveHead("Y", d);
        }

        private void arrowButtonZLPlus_Click(object sender, EventArgs e)
        {
            float d = ((ArrowButton)sender).CurrentValueF;
            if (ann.hasZHome && d + ann.zl > Main.printerSettings.PrintAreaHeight) d = Main.printerSettings.PrintAreaHeight - ann.zl;
            moveHead("L", d);

        }

        private void arrowButtonZRPlus_Click(object sender, EventArgs e)
        {
            float d = ((ArrowButton)sender).CurrentValueF;
            if (ann.hasZHome && d + ann.zr > Main.printerSettings.PrintAreaHeight) d = Main.printerSettings.PrintAreaHeight - ann.zr;
            moveHead("R", d);

        }

        private void arrowButtonZLMinus_Click(object sender, EventArgs e)
        {
            float d = -((ArrowButton)sender).CurrentValueF;
            if (FormPrinterSettings.ps.printerType != 3 && (ann.hasZHome && d + ann.zl < 0)) d = -ann.zl;
            moveHead("L", d);

        }

        private void arrowButtonZRMinus_Click(object sender, EventArgs e)
        {
            float d = -((ArrowButton)sender).CurrentValueF;
            if (FormPrinterSettings.ps.printerType != 3 && (ann.hasZHome && d + ann.zr < 0)) d = -ann.zr;
            moveHead("R", d);

        }

        private void sliderFlowrate_ValueChanged(object sender, EventArgs e)
        {
            if (!createCommands) return;
            //labelSpeed.Text = sliderSpeed.Value.ToString() + "%";
            int oldcon = con.flowMultiply;
            if (sender == sliderFlowrate)
            {
                if (con.flowMultiply != sliderFlowrate.Value)
                {
                    con.flowMultiply = sliderFlowrate.Value;
                    numericUpDownFlow.Value = con.flowMultiply;
                }
            }
            else
            {
                if (con.flowMultiply != numericUpDownFlow.Value)
                {
                    con.flowMultiply = (int)numericUpDownFlow.Value;
                    sliderFlowrate.Value = con.flowMultiply;
                }
            }
            if (oldcon != con.flowMultiply && con.connected && (con.isMarlin || con.isRepetier))
            {
                con.ignoreFeedback();
                con.injectManualCommand("M221 S" + sliderFlowrate.Value.ToString());
            }

        }

        private void PrintPanel_Load(object sender, EventArgs e)
        {

        }

        private void arrowButtonZPlus_Click(object sender, EventArgs e)
        {
            float d = ((ArrowButton)sender).CurrentValueF;
            if (ann.hasZHome && d + ann.zr > Main.printerSettings.PrintAreaHeight) d = Main.printerSettings.PrintAreaHeight - ann.zr;
            if (ann.hasZHome && d + ann.zl > Main.printerSettings.PrintAreaHeight) d = Main.printerSettings.PrintAreaHeight - ann.zl;

            moveHead("Z", d);
        }

        private void arrowButtonZMinus_Click(object sender, EventArgs e)
        {
            float d = -((ArrowButton)sender).CurrentValueF;
            if (FormPrinterSettings.ps.printerType != 3 && (ann.hasZHome && d + ann.zr < 0)) d = -ann.zr;
            if (FormPrinterSettings.ps.printerType != 3 && (ann.hasZHome && d + ann.zl < 0)) d = -ann.zl;
            moveHead("Z", d);

        }

     }
}
