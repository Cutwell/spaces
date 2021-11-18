from tkinter import Tk, mainloop, Frame, StringVar, messagebox
from tkinter.ttk import Style, Label, Button, OptionMenu
from os.path import exists
from os import getenv, getcwd, system, remove
import sys
from pickle import load, dump
from functools import partial

def gui(root, defaults):
    root.tk.call('source', 'theme/azure.tcl')
    Style().theme_use('azure')
    
    root.title("Spaces")

    outer_frame = Frame(root)
    outer_frame.pack(fill='both', expand=1, padx=10, pady=10)

    # inner frame
    inner_frame = Frame(outer_frame)

    # inner inner frames
    hotcorner_frame = Frame(inner_frame)    # options for hotcorners
    actions_frame   = Frame(inner_frame)    # add to startup / apply settings

    # options frames
    topleft_frame       = Frame(hotcorner_frame)
    topright_frame      = Frame(hotcorner_frame)
    bottomleft_frame    = Frame(hotcorner_frame)
    bottomright_frame   = Frame(hotcorner_frame)
    

    # add hotcorner options
    menu = ["Show desktop", "Show start menu", "Sleep", "Lock screen", "Show applications"]
    default_topleft, default_topright, default_bottomleft, default_bottomright = defaults

    topleft_stringvar       = StringVar(root, default_topleft)
    topleft_label           = Label(topleft_frame, text="Top left corner action: ")
    topleft_menu            = OptionMenu(topleft_frame, topleft_stringvar, default_topleft, *menu)

    topright_stringvar      = StringVar(root, default_topright)
    topright_label          = Label(topright_frame, text="Top right corner action: ")
    topright_menu           = OptionMenu(topright_frame, topright_stringvar, default_topright, *menu)

    bottomleft_stringvar    = StringVar(root, default_bottomleft)
    bottomleft_label        = Label(bottomleft_frame, text="Bottom left corner action: ")
    bottomleft_menu         = OptionMenu(bottomleft_frame, bottomleft_stringvar, default_bottomleft, *menu)

    bottomright_stringvar   = StringVar(root, default_bottomright)
    bottomright_label       = Label(bottomright_frame, text="Bottom right corner action: ")
    bottomright_menu        = OptionMenu(bottomright_frame, bottomright_stringvar, default_bottomright, *menu)

    # add action buttons
    startup_button  = Button(actions_frame, style="AccentButton", text="Add program to startup", command=add_to_startup)
    remove_button   = Button(actions_frame, style="AccentButton", text="Remove program from startup", command=remove_from_startup)
    apply_button    = Button(actions_frame, style="AccentButton", text="Apply changes", command=partial(apply_changes, topleft_stringvar, topright_stringvar, bottomleft_stringvar, bottomright_stringvar))

    # pack hotcorner options
    topleft_label.grid(row=0, column=0, padx=5, pady=5)
    topleft_menu.grid(row=0, column=1, padx=5, pady=5)

    topright_label.grid(row=0, column=0, padx=5, pady=5)
    topright_menu.grid(row=0, column=1, padx=5, pady=5)

    bottomleft_label.grid(row=0, column=0, padx=5, pady=5)
    bottomleft_menu.grid(row=0, column=1, padx=5, pady=5)

    bottomright_label.grid(row=0, column=0, padx=5, pady=5)
    bottomright_menu.grid(row=0, column=1, padx=5, pady=5)

    # pack hotcorner frames
    topleft_frame.pack(padx=10, pady=5)
    topright_frame.pack(padx=10, pady=5)
    bottomleft_frame.pack(padx=10, pady=5)
    bottomright_frame.pack(padx=10, pady=5)

    # pack action buttons
    startup_button.grid(row=0, column=0, padx=5, pady=5)
    remove_button.grid(row=0, column=1, padx=5, pady=5)
    apply_button.grid(row=0, column=2, padx=5, pady=5)

    # pack inner frames
    hotcorner_frame.pack(padx=10, pady=5)
    actions_frame.pack(padx=10, pady=5)

    # pack inner frame
    inner_frame.pack(expand=True, fill='both')

    mainloop()

def add_to_startup():
    startup_filepath = f'{getenv("APPDATA")}\\Microsoft\\Windows\\Start Menu\\Programs\\Startup'
    spaces_filepath = f'{getcwd()}\\spaces.pyw'
    python_exe_filepath = f"{getcwd()}\\venv\\Scripts\\pythonw.exe"
    batch = f"""@echo off\n\"{python_exe_filepath}\" \"{spaces_filepath}\""""
    vbs = f"CreateObject(\"Wscript.Shell\").Run \"{getcwd()}\\spaces.bat\",0,True"

    # batch file to run python script
    with open("spaces.bat", 'w') as file:
        file.write(batch)

    # visual basic file to run batch file silently
    with open(f"{startup_filepath}\\spaces.vbs", 'w') as file:
        file.write(vbs)

    messagebox.showinfo('Spaces', 'Program added to startup.')

def apply_changes(topleft_stringvar, topright_stringvar, bottomleft_stringvar, bottomright_stringvar):
    defaults = [topleft_stringvar.get(), topright_stringvar.get(), bottomleft_stringvar.get(), bottomright_stringvar.get()]

    with open("spaces.pkl", 'wb') as outfile:
        dump(defaults, outfile)

    messagebox.showinfo('Spaces', 'Changes saved.')

def remove_from_startup():
    startup_filepath = f'{getenv("APPDATA")}\\Microsoft\\Windows\\Start Menu\\Programs\\Startup'
    remove(f"{startup_filepath}\\spaces.bat")

    messagebox.showinfo('Spaces', 'Removed from startup.')

if __name__ == "__main__":
    # initialise defaults
    root = Tk()
    
    if exists("spaces.pkl"):
        with open("spaces.pkl", 'rb') as file:
            defaults = load(file)
    else:
        defaults = ["Show applications", "Show desktop", "Show start menu", "Lock screen"]

    gui(root, defaults) # user interface, for editing settings