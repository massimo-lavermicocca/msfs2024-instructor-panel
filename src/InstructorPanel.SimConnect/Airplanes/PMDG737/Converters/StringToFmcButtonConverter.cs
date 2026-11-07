using InstructorPanel.SimConnect.Airplanes.PMDG737.Models;

namespace InstructorPanel.SimConnect.Airplanes.PMDG737.Converters
{
    public class StringToFmcButtonConverter
    {
        public PMDG_FMC_BUTTONS[] Convert(string stringToConvert)
        {
            var convertedString = new List<PMDG_FMC_BUTTONS>();

            if (string.IsNullOrEmpty(stringToConvert))
            {
                return [.. convertedString];
            }

            foreach (var button in stringToConvert)
            {
                switch (button)
                {
                    case '1':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_1);
                        break;
                    case '2':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_2);
                        break;
                    case '3':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_3);
                        break;
                    case '4':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_4);
                        break;
                    case '5':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_5);
                        break;
                    case '6':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_6);
                        break;
                    case '7':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_7);
                        break;
                    case '8':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_8);
                        break;
                    case '9':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_9);
                        break;
                    case '.':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_DOT);
                        break;
                    case '0':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_0);
                        break;
                    case '+':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_PLUS_MINUS);
                        break;
                    case 'A':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_A);
                        break;
                    case 'B':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_B);
                        break;
                    case 'C':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_C);
                        break;
                    case 'D':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_D);
                        break;
                    case 'E':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_E);
                        break;
                    case 'F':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_F);
                        break;
                    case 'G':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_G);
                        break;
                    case 'H':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_H);
                        break;
                    case 'I':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_I);
                        break;
                    case 'J':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_J);
                        break;
                    case 'K':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_K);
                        break;
                    case 'L':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_L);
                        break;
                    case 'M':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_M);
                        break;
                    case 'N':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_N);
                        break;
                    case 'O':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_O);
                        break;
                    case 'P':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_P);
                        break;
                    case 'Q':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_Q);
                        break;
                    case 'R':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_R);
                        break;
                    case 'S':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_S);
                        break;
                    case 'T':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_T);
                        break;
                    case 'U':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_U);
                        break;
                    case 'V':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_V);
                        break;
                    case 'W':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_W);
                        break;
                    case 'X':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_X);
                        break;
                    case 'Y':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_Y);
                        break;
                    case 'Z':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_Z);
                        break;
                    case ' ':
                        convertedString.Add(PMDG_FMC_BUTTONS.EVT_CDU_L_SPACE);
                        break;
                }
            }
            ;

            return [.. convertedString];
        }
    }
}
