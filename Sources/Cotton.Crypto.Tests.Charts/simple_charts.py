import re
import matplotlib.pyplot as plt
import pandas as pd
import numpy as np

def parse_test_results(filename):
    """Парсит результаты тестов производительности из файла"""
    
    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Находим секции с результатами
    encrypt_section = re.search(r'=== ENCRYPTION THREAD/CHUNK SWEEP ===(.*?)(?===|$)', content, re.DOTALL)
    decrypt_section = re.search(r'=== DECRYPTION THREAD/CHUNK SWEEP ===(.*?)(?===|$)', content, re.DOTALL)
    
    def extract_data(section_text):
        """Извлекает данные из текстовой секции"""
        if not section_text:
            return pd.DataFrame()
        
        # Ищем строки с данными (формат: число | число | число.число)
        pattern = r'(\d+)\s*\|\s*(\d+)\s*\|\s*([\d.]+)'
        matches = re.findall(pattern, section_text)
        
        data = []
        for match in matches:
            threads = int(match[0])
            chunk_mb = int(match[1])
            throughput = float(match[2])
            data.append({'Threads': threads, 'ChunkMB': chunk_mb, 'Throughput': throughput})
        
        return pd.DataFrame(data)
    
    encrypt_data = extract_data(encrypt_section.group(1) if encrypt_section else "")
    decrypt_data = extract_data(decrypt_section.group(1) if decrypt_section else "")
    
    return encrypt_data, decrypt_data

def create_plots(encrypt_data, decrypt_data):
    """Создает четыре графика с улучшенным стилем"""
    
    # Настраиваем стиль matplotlib
    plt.rcParams['figure.facecolor'] = 'white'
    plt.rcParams['axes.facecolor'] = 'white'
    plt.rcParams['axes.grid'] = True
    plt.rcParams['grid.alpha'] = 0.3
    
    fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(16, 12))
    fig.suptitle('Performance Analysis: Encryption/Decryption Throughput', 
                 fontsize=16, fontweight='bold', y=0.98)
    
    # Цветовые схемы
    thread_colors = ['#1f77b4', '#ff7f0e', '#2ca02c', '#d62728', '#9467bd', '#8c564b']
    chunk_colors = ['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', '#ffff33', '#a65628']
    
    # График 1: Encrypt - throughput vs chunk size (по разным числам потоков)
    unique_threads = sorted(encrypt_data['Threads'].unique())
    for i, threads in enumerate(unique_threads):
        thread_data = encrypt_data[encrypt_data['Threads'] == threads].sort_values('ChunkMB')
        ax1.plot(thread_data['ChunkMB'], thread_data['Throughput'], 
                marker='o', label=f'{threads} threads', linewidth=2.5, 
                markersize=8, color=thread_colors[i % len(thread_colors)])
    
    ax1.set_xlabel('Chunk Size (MB)', fontsize=12, fontweight='bold')
    ax1.set_ylabel('Throughput (MB/s)', fontsize=12, fontweight='bold')
    ax1.set_title('Encryption: Throughput vs Chunk Size', fontsize=14, fontweight='bold')
    ax1.legend(frameon=True, fancybox=True, shadow=True)
    ax1.grid(True, alpha=0.3, linestyle='-', linewidth=0.5)
    
    # Добавляем подписи осей с размерами чанков
    chunk_ticks = sorted(encrypt_data['ChunkMB'].unique())
    ax1.set_xticks(chunk_ticks)
    ax1.set_xticklabels([f'{int(x)}' for x in chunk_ticks])
    
    # График 2: Decrypt - throughput vs chunk size (по разным числам потоков)
    for i, threads in enumerate(unique_threads):
        thread_data = decrypt_data[decrypt_data['Threads'] == threads].sort_values('ChunkMB')
        ax2.plot(thread_data['ChunkMB'], thread_data['Throughput'], 
                marker='s', label=f'{threads} threads', linewidth=2.5, 
                markersize=8, color=thread_colors[i % len(thread_colors)])
    
    ax2.set_xlabel('Chunk Size (MB)', fontsize=12, fontweight='bold')
    ax2.set_ylabel('Throughput (MB/s)', fontsize=12, fontweight='bold')
    ax2.set_title('Decryption: Throughput vs Chunk Size', fontsize=14, fontweight='bold')
    ax2.legend(frameon=True, fancybox=True, shadow=True)
    ax2.grid(True, alpha=0.3, linestyle='-', linewidth=0.5)
    ax2.set_xticks(chunk_ticks)
    ax2.set_xticklabels([f'{int(x)}' for x in chunk_ticks])
    
    # График 3: Encrypt - throughput vs threads (по разным размерам чанков)
    unique_chunks = sorted(encrypt_data['ChunkMB'].unique())
    
    for i, chunk_size in enumerate(unique_chunks):
        chunk_data = encrypt_data[encrypt_data['ChunkMB'] == chunk_size].sort_values('Threads')
        ax3.plot(chunk_data['Threads'], chunk_data['Throughput'], 
                marker='o', label=f'{int(chunk_size)}MB', linewidth=2.5, 
                markersize=8, color=chunk_colors[i % len(chunk_colors)])
    
    ax3.set_xlabel('Number of Threads', fontsize=12, fontweight='bold')
    ax3.set_ylabel('Throughput (MB/s)', fontsize=12, fontweight='bold')
    ax3.set_title('Encryption: Throughput vs Threads', fontsize=14, fontweight='bold')
    ax3.legend(frameon=True, fancybox=True, shadow=True, title='Chunk Size')
    ax3.grid(True, alpha=0.3, linestyle='-', linewidth=0.5)
    ax3.set_xticks(unique_threads)
    ax3.set_xticklabels([str(int(x)) for x in unique_threads])
    
    # График 4: Decrypt - throughput vs threads (по разным размерам чанков)
    for i, chunk_size in enumerate(unique_chunks):
        chunk_data = decrypt_data[decrypt_data['ChunkMB'] == chunk_size].sort_values('Threads')
        ax4.plot(chunk_data['Threads'], chunk_data['Throughput'], 
                marker='s', label=f'{int(chunk_size)}MB', linewidth=2.5, 
                markersize=8, color=chunk_colors[i % len(chunk_colors)])
    
    ax4.set_xlabel('Number of Threads', fontsize=12, fontweight='bold')
    ax4.set_ylabel('Throughput (MB/s)', fontsize=12, fontweight='bold')
    ax4.set_title('Decryption: Throughput vs Threads', fontsize=14, fontweight='bold')
    ax4.legend(frameon=True, fancybox=True, shadow=True, title='Chunk Size')
    ax4.grid(True, alpha=0.3, linestyle='-', linewidth=0.5)
    ax4.set_xticks(unique_threads)
    ax4.set_xticklabels([str(int(x)) for x in unique_threads])
    
    # Улучшаем внешний вид
    for ax in [ax1, ax2, ax3, ax4]:
        ax.spines['top'].set_visible(False)
        ax.spines['right'].set_visible(False)
        ax.spines['left'].set_linewidth(0.5)
        ax.spines['bottom'].set_linewidth(0.5)
    
    plt.tight_layout()
    return fig

def print_summary(encrypt_data, decrypt_data):
    """Выводит краткую сводку"""
    
    print("\n" + "="*50)
    print("КРАТКАЯ СВОДКА АНАЛИЗА")
    print("="*50)
    
    # Находим лучшие результаты
    encrypt_best = encrypt_data.loc[encrypt_data['Throughput'].idxmax()]
    decrypt_best = decrypt_data.loc[decrypt_data['Throughput'].idxmax()]
    
    print(f"\n🏆 ЛУЧШИЕ РЕЗУЛЬТАТЫ:")
    print(f"   Encryption: {encrypt_best['Throughput']:.1f} MB/s")
    print(f"   ({encrypt_best['Threads']:.0f} потоков, {encrypt_best['ChunkMB']:.0f}MB чанки)")
    print(f"   Decryption: {decrypt_best['Throughput']:.1f} MB/s")
    print(f"   ({decrypt_best['Threads']:.0f} потоков, {decrypt_best['ChunkMB']:.0f}MB чанки)")
    
    print(f"\n📊 СРЕДНИЕ ЗНАЧЕНИЯ:")
    print(f"   Encryption: {encrypt_data['Throughput'].mean():.1f} MB/s")
    print(f"   Decryption: {decrypt_data['Throughput'].mean():.1f} MB/s")
    print(f"   Decryption на {((decrypt_data['Throughput'].mean() / encrypt_data['Throughput'].mean() - 1) * 100):.1f}% быстрее")
    
    print("\n" + "="*50)

def main():
    """Основная функция"""
    try:
        # Парсим данные из файла
        encrypt_data, decrypt_data = parse_test_results('input.txt')
        
        if encrypt_data.empty or decrypt_data.empty:
            print("Ошибка: не удалось найти данные в файле input.txt")
            return
        
        print(f"✅ Данные успешно загружены:")
        print(f"   Encryption: {len(encrypt_data)} записей")
        print(f"   Decryption: {len(decrypt_data)} записей")
        
        # Создаем графики
        fig = create_plots(encrypt_data, decrypt_data)
        
        # Сохраняем графики
        fig.savefig('performance_charts.png', dpi=300, bbox_inches='tight', 
                   facecolor='white', edgecolor='none')
        print(f"\n💾 Графики сохранены в performance_charts.png")
        
        # Выводим сводку
        print_summary(encrypt_data, decrypt_data)
        
        # Показываем графики
        plt.show()
        
        print(f"\n📈 Созданы следующие графики:")
        print(f"   1. Encryption: Throughput vs Chunk Size")
        print(f"   2. Decryption: Throughput vs Chunk Size") 
        print(f"   3. Encryption: Throughput vs Threads")
        print(f"   4. Decryption: Throughput vs Threads")
        
    except FileNotFoundError:
        print("❌ Ошибка: файл 'input.txt' не найден")
    except Exception as e:
        print(f"❌ Ошибка: {e}")

if __name__ == "__main__":
    main()