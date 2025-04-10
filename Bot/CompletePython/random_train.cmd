@echo off
call C:\Users\ralph\miniconda3\Scripts\activate.bat swoc
python random_train.py
conda deactivate
